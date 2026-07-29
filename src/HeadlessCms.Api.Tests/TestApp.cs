using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FastEndpoints;
using FastEndpoints.Security;
using FastEndpoints.Testing;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Auth.Services;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Endpoints.Auth;
using HeadlessCms.Api.Models;
using HeadlessCms.Api.Tenancy.Models;
using HeadlessCms.Api.Tenancy.Services;
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
    public const string SigningKey =
        "test-only-signing-key-that-is-long-enough-for-hmac-sha256-validation";

    private readonly PostgreSqlContainer container =
        new PostgreSqlBuilder(
            Environment.GetEnvironmentVariable("TEST_POSTGRES_IMAGE")
            ?? "postgres:18-alpine")
        .Build();

    public HttpClient HttpsClient { get; private set; } = null!;
    public string PostgreSqlConnectionString => container.GetConnectionString();

    protected override async ValueTask PreSetupAsync()
    {
        await container.StartAsync();
    }

    protected override void ConfigureApp(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
    }

    protected override void ConfigureServices(IServiceCollection services)
    {
        services.RemoveAll<ApplicationDbContext>();
        services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
        services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
        services.AddDbContext<ApplicationDbContext>(
            options => options.UseNpgsql(container.GetConnectionString()));

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
                BaseAddress = new Uri("https://localhost")
            });

        return ValueTask.CompletedTask;
    }

    protected override async ValueTask TearDownAsync()
    {
        HttpsClient?.Dispose();
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

    public Task<Tenant> SeedTenantAsync(
        params (User User, TenantRole Role)[] members)
    {
        return WithDatabaseAsync(
            async db =>
            {
                var tenant = new Tenant
                {
                    Name = $"Tenant {Guid.NewGuid():N}",
                    Memberships = members
                        .Select(member => new TenantMembership
                        {
                            UserId = member.User.Id,
                            Role = member.Role
                        })
                        .ToList()
                };

                db.Tenants.Add(tenant);
                await db.SaveChangesAsync();
                return tenant;
            });
    }

    public Task<Project> SeedProjectAsync(Guid tenantId, string name)
    {
        return WithDatabaseAsync(
            async db =>
            {
                var now = DateTime.UtcNow;
                var project = new Project
                {
                    TenantId = tenantId,
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
        var (response, tokens) =
            await HttpsClient.POSTAsync<Login, LoginRequest, TokenResponse>(
                new LoginRequest
                {
                    Email = AsEmail(identifier),
                    Password = password
                });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return tokens.AccessToken;
    }

    public async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        string accessToken,
        object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
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

    public static string ProjectsPath(Guid tenantId) =>
        $"/api/tenants/{tenantId}/projects";

    public static string ProjectPath(Guid tenantId, Guid projectId) =>
        $"{ProjectsPath(tenantId)}/{projectId}";
}

public sealed record SentInvitation(
    string Email,
    string TenantName,
    TenantRole Role,
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
        string tenantName,
        TenantRole role,
        string token,
        CancellationToken ct = default)
    {
        lock (sent)
            sent.Add(new SentInvitation(email, tenantName, role, token));
        return Task.CompletedTask;
    }

    public void Clear()
    {
        lock (sent)
            sent.Clear();
    }
}
