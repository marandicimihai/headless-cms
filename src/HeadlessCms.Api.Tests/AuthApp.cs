using HeadlessCms.Api.Data;
using HeadlessCms.Api.Tenancy.Models;
using HeadlessCms.Api.Tenancy.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HeadlessCms.Api.Tests;

public sealed class AuthApp : ApiApp
{
    public const string SigningKey =
        "test-only-signing-key-that-is-long-enough-for-hmac-sha256-validation";

    private readonly string databaseName = $"headless-cms-auth-tests-{Guid.NewGuid()}";

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

    public override async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
        Services.GetRequiredService<TestInvitationEmailSender>().Clear();
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
