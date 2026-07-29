using FastEndpoints.Testing;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Auth.Services;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Tenancy.Models;
using HeadlessCms.Api.Tenancy.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HeadlessCms.Api.Tests;

public sealed class AuthApp : AppFixture<Program>
{
    public const string SigningKey =
        "test-only-signing-key-that-is-long-enough-for-hmac-sha256-validation";

    private readonly string databaseName = $"headless-cms-auth-tests-{Guid.NewGuid()}";

    public HttpClient HttpsClient { get; private set; } = null!;

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
            options => options.UseInMemoryDatabase(databaseName));
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

    protected override ValueTask TearDownAsync()
    {
        HttpsClient?.Dispose();
        return ValueTask.CompletedTask;
    }

    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
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
