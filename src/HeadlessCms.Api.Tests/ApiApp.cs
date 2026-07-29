using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FastEndpoints;
using FastEndpoints.Security;
using FastEndpoints.Testing;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Auth.Services;
using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Endpoints.Auth;
using HeadlessCms.Api.Tenancy.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace HeadlessCms.Api.Tests;

public abstract class ApiApp : AppFixture<Program>
{
    public HttpClient HttpsClient { get; private set; } = null!;

    protected override void ConfigureApp(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
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

    protected override ValueTask TearDownAsync()
    {
        HttpsClient?.Dispose();
        return ValueTask.CompletedTask;
    }

    public abstract Task ResetDatabaseAsync();

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
