using HeadlessCms.Api.Caching;
using StackExchange.Redis;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using System.Net;
using System.Net.Http.Json;
using FastEndpoints;
using FastEndpoints.Testing;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Auth.Services;
using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Endpoints.Auth;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Testcontainers.PostgreSql;

namespace HeadlessCms.Api.Tests;

public sealed class TestApp : AppFixture<Program>
{
    private readonly PostgreSqlContainer container =
        new PostgreSqlBuilder(
            Environment.GetEnvironmentVariable("TEST_POSTGRES_IMAGE")
            ?? "postgres:18-alpine")
        .Build();

    private readonly IContainer redisContainer = new ContainerBuilder("redis:7-alpine")
        .WithPortBinding(6379, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(6379))
        .Build();
    private IConnectionMultiplexer redis = null!;
    public HttpClient HttpsClient { get; private set; } = null!;
    public string PostgreSqlConnectionString => container.GetConnectionString();

    protected override async ValueTask PreSetupAsync()
    {
        await container.StartAsync();
        await redisContainer.StartAsync();
        redis = await ConnectionMultiplexer.ConnectAsync($"{redisContainer.Hostname}:{redisContainer.GetMappedPublicPort(6379)}");
    }

    protected override void ConfigureApp(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
    }

    protected override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<ResourceCacheOptions>(options =>
        {
            options.Enabled = true;
            options.KeyPrefix = $"tests:{Guid.NewGuid():N}";
        });
        services.AddSingleton(redis);
        services.AddSingleton<ResourceQueryCounter>();
        services.RemoveAll<ApplicationDbContext>();
        services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
        services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
        services.AddDbContext<ApplicationDbContext>(
            (provider, options) => options.UseNpgsql(container.GetConnectionString())
                .AddInterceptors(provider.GetRequiredService<ResourceQueryCounter>()));

        services.RemoveAll<IInvitationEmailSender>();
        services.AddSingleton<TestInvitationEmailSender>();
        services.AddSingleton<IInvitationEmailSender>(
            provider => provider.GetRequiredService<TestInvitationEmailSender>());
    }

    protected override ValueTask SetupAsync()
    {
        HttpsClient = CreateClient(
            new ClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = false,
                BaseAddress = new Uri("https://localhost")
            });

        return ValueTask.CompletedTask;
    }

    protected override async ValueTask TearDownAsync()
    {
        HttpsClient?.Dispose();
        redis.Dispose();
        await redisContainer.DisposeAsync();
        await container.DisposeAsync();
    }

    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.ExecuteSqlRawAsync(
            """
            DROP SCHEMA IF EXISTS public CASCADE;
            CREATE SCHEMA public;
            """);
        await db.Database.MigrateAsync();
        Services.GetRequiredService<TestInvitationEmailSender>().Clear();
    }

    public async Task<User> SeedUserAsync(
        string identifier,
        string password,
        PlatformRole platformRole = PlatformRole.User,
        string? email = null)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

        var user = new User
        {
            Email = EmailNormalizer.Normalize(email ?? AsEmail(identifier)),
            PlatformRole = platformRole
        };
        user.PasswordHash = hasher.HashPassword(user, password);

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return user;
    }

    public static string AsEmail(string value) =>
        value.Contains('@', StringComparison.Ordinal)
            ? value
            : $"{value}@example.test";

    public Task<Workspace> SeedWorkspaceAsync(
        params (User User, WorkspaceRole Role)[] members)
    {
        return WithDatabaseAsync(
            async db =>
            {
                var workspace = new Workspace
                {
                    Name = $"Workspace {Guid.NewGuid():N}",
                    Memberships = members
                        .Select(member => new WorkspaceMembership
                        {
                            UserId = member.User.Id,
                            Role = member.Role
                        })
                        .ToList()
                };

                db.Workspaces.Add(workspace);
                await db.SaveChangesAsync();
                return workspace;
            });
    }

    public Task<Project> SeedProjectAsync(Guid workspaceId, string name)
    {
        return WithDatabaseAsync(
            async db =>
            {
                var now = DateTime.UtcNow;
                var project = new Project
                {
                    WorkspaceId = workspaceId,
                    Name = name,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                db.Projects.Add(project);
                await db.SaveChangesAsync();
                return project;
            });
    }

    public async Task<string> LoginAsync(string identifier, string password)
    {
        var (response, _) =
            await HttpsClient.POSTAsync<Login, LoginRequest, AuthSessionResponse>(
                new LoginRequest
                {
                    Email = AsEmail(identifier),
                    Password = password
                });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return ExtractSessionCookie(response);
    }

    public static string ExtractSessionCookie(HttpResponseMessage response)
    {
        response.Headers.TryGetValues("Set-Cookie", out var values).ShouldBeTrue();
        var header = values!.Single(value =>
            value.StartsWith(
                $"{AuthSessionService.CookieName}=",
                StringComparison.Ordinal));
        return header.Split(';', 2)[0];
    }

    public async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        string sessionCookie,
        object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("Cookie", sessionCookie);
        if (body is not null)
            request.Content = JsonContent.Create(body);

        return await HttpsClient.SendAsync(request);
    }

    public async Task<TResult> WithDatabaseAsync<TResult>(
        Func<ApplicationDbContext, Task<TResult>> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await action(db);
    }

    public async Task<TResult> WithServiceAsync<TService, TResult>(
        Func<TService, Task<TResult>> action)
        where TService : notnull
    {
        using var scope = Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<TService>();
        return await action(service);
    }

    public static string ProjectsPath(Guid workspaceId) =>
        $"/api/workspaces/{workspaceId}/projects";

    public static string ProjectPath(Guid workspaceId, Guid projectId) =>
        $"{ProjectsPath(workspaceId)}/{projectId}";
}

public sealed record SentInvitation(
    string Email,
    string WorkspaceName,
    WorkspaceRole Role,
    string Token);

public sealed class TestInvitationEmailSender : IInvitationEmailSender
{
    private readonly List<SentInvitation> sent = [];

    public IReadOnlyList<SentInvitation> Sent
    {
        get
        {
            lock (sent)
                return sent.ToList();
        }
    }

    public Task SendAsync(
        string email,
        string workspaceName,
        WorkspaceRole role,
        string token,
        CancellationToken ct = default)
    {
        lock (sent)
            sent.Add(new SentInvitation(email, workspaceName, role, token));
        return Task.CompletedTask;
    }

    public void Clear()
    {
        lock (sent)
            sent.Clear();
    }
}
