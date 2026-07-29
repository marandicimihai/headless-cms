using FastEndpoints.Testing;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Endpoints.Auth;
using HeadlessCms.Api.Tenancy.Models;
using HeadlessCms.Api.Tenancy.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace HeadlessCms.Api.Tests;

[Collection<TestAppCollection>]
public sealed class InfrastructureTests(TestApp app) : TestBase
{
    protected override async ValueTask SetupAsync()
    {
        await app.ResetDatabaseAsync();
    }

    [Fact]
    public async Task SeedPlatformAdminUser_InProduction_CreatesConfiguredAdmin()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var productionApp = CreateProductionSeedApp(
            " New.Admin@Example.Test ",
            "admin-password");

        await productionApp.SeedPlatformAdminUser();

        var admin = await app.WithDatabaseAsync(
            db => db.Users.AsNoTracking().SingleAsync(ct));
        admin.Email.ShouldBe("new.admin@example.test");
        admin.PlatformRole.ShouldBe(PlatformRole.PlatformAdmin);

        var verification = new PasswordHasher<User>().VerifyHashedPassword(
            admin,
            admin.PasswordHash,
            "admin-password");
        verification.ShouldNotBe(PasswordVerificationResult.Failed);
    }

    [Theory]
    [InlineData(
        "existing@example.test",
        " Existing@Example.Test ",
        null)]
    [InlineData(
        "legacy-admin@legacy.invalid",
        "promoted@example.test",
        " Legacy-Admin ")]
    public async Task SeedPlatformAdminUser_InProduction_PromotesExistingOrLegacyUser(
        string existingEmail,
        string configuredAdminEmail,
        string? legacyUsername)
    {
        var ct = TestContext.Current.CancellationToken;
        var existing = await app.SeedUserAsync(
            existingEmail,
            "existing-password");
        var originalPasswordHash = existing.PasswordHash;
        await using var productionApp = CreateProductionSeedApp(
            configuredAdminEmail,
            "unused-admin-password",
            legacyUsername);

        await productionApp.SeedPlatformAdminUser();

        var users = await app.WithDatabaseAsync(
            db => db.Users.AsNoTracking().ToListAsync(ct));
        var promoted = users.Single();
        promoted.Id.ShouldBe(existing.Id);
        promoted.Email.ShouldBe(configuredAdminEmail.Trim().ToLowerInvariant());
        promoted.PlatformRole.ShouldBe(PlatformRole.PlatformAdmin);
        promoted.PasswordHash.ShouldBe(originalPasswordHash);
    }

    [Theory]
    [InlineData(null, "https://localhost/invitations/accept")]
    [InlineData(
        "https://cms.example.test/accept",
        "https://cms.example.test/accept")]
    public async Task LoggingInvitationEmailSender_LogsEscapedInvitationUrl(
        string? configuredBaseUrl,
        string expectedBaseUrl)
    {
        var logger = new RecordingLogger<LoggingInvitationEmailSender>();
        var configurationValues = new Dictionary<string, string?>();
        if (configuredBaseUrl is not null)
            configurationValues["Tenancy:InvitationUrl"] = configuredBaseUrl;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();
        var sender = new LoggingInvitationEmailSender(logger, configuration);

        await sender.SendAsync(
            "invitee@example.test",
            "Tenant A",
            TenantRole.Editor,
            "a+b/c?=",
            TestContext.Current.CancellationToken);

        var entry = logger.Entries.Single();
        entry.Level.ShouldBe(LogLevel.Information);
        entry.Exception.ShouldBeNull();
        entry.Message.ShouldBe(
            "Development invitation for invitee@example.test to Tenant A as Editor: " +
            $"{expectedBaseUrl}?token=a%2Bb%2Fc%3F%3D");
    }

    [Fact]
    public async Task UnconfiguredInvitationEmailSender_RejectsProductionDelivery()
    {
        var sender = new UnconfiguredInvitationEmailSender();

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await sender.SendAsync(
                "invitee@example.test",
                "Tenant A",
                TenantRole.Member,
                "token",
                TestContext.Current.CancellationToken));

        exception.Message.ShouldBe(
            "No production invitation email sender has been configured.");
    }

    [Theory]
    [InlineData(
        null,
        "Required configuration 'Tenancy:InvitationExpirationHours' is missing.")]
    [InlineData(
        "0",
        "Tenancy invitation expiration must be greater than zero.")]
    [InlineData(
        "-1",
        "Tenancy invitation expiration must be greater than zero.")]
    public async Task TenantInvitationService_RejectsMissingOrInvalidExpiration(
        string? configuredHours,
        string expectedMessage)
    {
        var configurationValues = new Dictionary<string, string?>();
        if (configuredHours is not null)
        {
            configurationValues["Tenancy:InvitationExpirationHours"] =
                configuredHours;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();
        using var scope = app.Services.CreateScope();
        var service = new TenantInvitationService(
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(),
            configuration,
            scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>(),
            scope.ServiceProvider.GetRequiredService<IInvitationEmailSender>());
        var pendingInvitation = new TenantInvitation
        {
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => service.ResendAsync(
                pendingInvitation,
                TestContext.Current.CancellationToken));

        exception.Message.ShouldBe(expectedMessage);
    }

    [Fact]
    public void Refresh_WithoutRefreshTokenLifetime_Throws()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Auth:SigningKey"] = TestApp.SigningKey,
                    ["Auth:AccessTokenExpirationMinutes"] = "10"
                })
            .Build();
        using var scope = app.Services.CreateScope();

        var exception = Should.Throw<InvalidOperationException>(
            () => new Refresh(
                configuration,
                new RecordingLogger<Refresh>(),
                scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>()));

        exception.Message.ShouldBe(
            "Required configuration 'Auth:RefreshTokenExpirationDays' is missing.");
    }

    [Fact]
    public void Refresh_WithoutAccessTokenLifetime_Throws()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Auth:SigningKey"] = TestApp.SigningKey,
                    ["Auth:RefreshTokenExpirationDays"] = "7"
                })
            .Build();
        using var scope = app.Services.CreateScope();

        var exception = Should.Throw<InvalidOperationException>(
            () => new Refresh(
                configuration,
                new RecordingLogger<Refresh>(),
                scope.ServiceProvider
                    .GetRequiredService<ApplicationDbContext>()));

        exception.Message.ShouldBe(
            "Required configuration 'Auth:AccessTokenExpirationMinutes' is missing.");
    }

    [Fact]
    public async Task ApplicationDbContextFactory_UsesEnvironmentConnectionString()
    {
        var ct = TestContext.Current.CancellationToken;
        var originalDirectory = Directory.GetCurrentDirectory();
        var originalEnvironment =
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var originalConnectionString =
            Environment.GetEnvironmentVariable(
                "ConnectionStrings__DefaultConnection");

        try
        {
            Directory.SetCurrentDirectory(AppContext.BaseDirectory);
            Environment.SetEnvironmentVariable(
                "ASPNETCORE_ENVIRONMENT",
                "Testing");
            Environment.SetEnvironmentVariable(
                "ConnectionStrings__DefaultConnection",
                app.PostgreSqlConnectionString);

            await using var db =
                new ApplicationDbContextFactory().CreateDbContext([]);

            db.Database.ProviderName.ShouldBe(
                "Npgsql.EntityFrameworkCore.PostgreSQL");
            db.Database.GetConnectionString().ShouldBe(
                app.PostgreSqlConnectionString);
            (await db.Database.CanConnectAsync(ct)).ShouldBeTrue();
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                "ConnectionStrings__DefaultConnection",
                originalConnectionString);
            Environment.SetEnvironmentVariable(
                "ASPNETCORE_ENVIRONMENT",
                originalEnvironment);
            Directory.SetCurrentDirectory(originalDirectory);
        }
    }

    [Fact]
    public void ApplicationDbContextFactory_WithoutConnectionString_Throws()
    {
        var originalDirectory = Directory.GetCurrentDirectory();
        var originalEnvironment =
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var originalConnectionString =
            Environment.GetEnvironmentVariable(
                "ConnectionStrings__DefaultConnection");

        try
        {
            Directory.SetCurrentDirectory(AppContext.BaseDirectory);
            Environment.SetEnvironmentVariable(
                "ASPNETCORE_ENVIRONMENT",
                $"NoConnection-{Guid.NewGuid():N}");
            Environment.SetEnvironmentVariable(
                "ConnectionStrings__DefaultConnection",
                null);

            var exception = Should.Throw<InvalidOperationException>(
                () => new ApplicationDbContextFactory().CreateDbContext([]));

            exception.Message.ShouldBe(
                "Connection string 'DefaultConnection' is required for EF Core tooling.");
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                "ConnectionStrings__DefaultConnection",
                originalConnectionString);
            Environment.SetEnvironmentVariable(
                "ASPNETCORE_ENVIRONMENT",
                originalEnvironment);
            Directory.SetCurrentDirectory(originalDirectory);
        }
    }

    [Fact]
    public async Task PostgreSql_EnforcesUniqueUserEmail()
    {
        var ct = TestContext.Current.CancellationToken;
        await app.SeedUserAsync(
            "duplicate@example.test",
            "first-password");

        await Should.ThrowAsync<DbUpdateException>(
            () => app.SeedUserAsync(
                " DUPLICATE@EXAMPLE.TEST ",
                "second-password"));

        var storedEmails = await app.WithDatabaseAsync(
            db => db.Users.AsNoTracking()
                .Select(user => user.Email)
                .ToListAsync(ct));
        storedEmails.ShouldBe(["duplicate@example.test"]);
    }

    [Fact]
    public async Task PostgreSql_AllowsOnlyOnePlatformAdmin()
    {
        var ct = TestContext.Current.CancellationToken;
        await app.SeedUserAsync(
            "first-admin@example.test",
            "first-password",
            PlatformRole.PlatformAdmin);

        await Should.ThrowAsync<DbUpdateException>(
            () => app.SeedUserAsync(
                "second-admin@example.test",
                "second-password",
                PlatformRole.PlatformAdmin));

        var admins = await app.WithDatabaseAsync(
            db => db.Users.AsNoTracking()
                .Where(user => user.PlatformRole == PlatformRole.PlatformAdmin)
                .ToListAsync(ct));
        admins.Count.ShouldBe(1);
        admins[0].Email.ShouldBe("first-admin@example.test");
    }

    [Fact]
    public async Task PostgreSql_AllowsOnlyOneOwnerPerTenant()
    {
        var ct = TestContext.Current.CancellationToken;
        var firstOwner = await app.SeedUserAsync(
            "first-owner@example.test",
            "first-password");
        var secondOwner = await app.SeedUserAsync(
            "second-owner@example.test",
            "second-password");

        await Should.ThrowAsync<DbUpdateException>(
            () => app.SeedTenantAsync(
                (firstOwner, TenantRole.Owner),
                (secondOwner, TenantRole.Owner)));

        var tenantCount = await app.WithDatabaseAsync(
            db => db.Tenants.CountAsync(ct));
        tenantCount.ShouldBe(0);
    }

    [Fact]
    public async Task PostgreSql_EnforcesTenantMembershipCompositeKey()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = await app.SeedUserAsync(
            "member@example.test",
            "member-password");
        var tenant = await app.SeedTenantAsync((user, TenantRole.Member));

        await Should.ThrowAsync<DbUpdateException>(
            () => app.WithDatabaseAsync(
                async db =>
                {
                    db.TenantMemberships.Add(
                        new TenantMembership
                        {
                            TenantId = tenant.Id,
                            UserId = user.Id,
                            Role = TenantRole.Editor
                        });
                    await db.SaveChangesAsync(ct);
                    return true;
                }));

        var membership = await app.WithDatabaseAsync(
            db => db.TenantMemberships.AsNoTracking().SingleAsync(ct));
        membership.Role.ShouldBe(TenantRole.Member);
    }

    [Fact]
    public async Task PostgreSql_EnforcesInvitationRelationshipsAndUniqueTokenHash()
    {
        var ct = TestContext.Current.CancellationToken;
        var inviter = await app.SeedUserAsync(
            "inviter@example.test",
            "inviter-password");
        var tenant = await app.SeedTenantAsync((inviter, TenantRole.Owner));

        await Should.ThrowAsync<DbUpdateException>(
            () => SeedInvitationAsync(
                tenant.Id,
                $"missing-user-{Guid.NewGuid():N}",
                new string('a', 64),
                "orphan@example.test"));

        var invitation = await SeedInvitationAsync(
            tenant.Id,
            inviter.Id,
            new string('b', 64),
            "first@example.test");

        await Should.ThrowAsync<DbUpdateException>(
            () => SeedInvitationAsync(
                tenant.Id,
                inviter.Id,
                invitation.TokenHash,
                "second@example.test"));

        await Should.ThrowAsync<DbUpdateException>(
            () => app.WithDatabaseAsync(
                async db =>
                {
                    var storedInviter = await db.Users.SingleAsync(
                        user => user.Id == inviter.Id,
                        ct);
                    db.Users.Remove(storedInviter);
                    await db.SaveChangesAsync(ct);
                    return true;
                }));

        var invitationStillExists = await app.WithDatabaseAsync(
            db => db.TenantInvitations.AnyAsync(
                candidate => candidate.Id == invitation.Id,
                ct));
        invitationStillExists.ShouldBeTrue();
    }

    [Fact]
    public async Task PostgreSql_EnforcesAcceptedByUserRelationshipAndRestrictedDelete()
    {
        var ct = TestContext.Current.CancellationToken;
        var inviter = await app.SeedUserAsync(
            "inviter@example.test",
            "inviter-password");
        var acceptedBy = await app.SeedUserAsync(
            "accepted@example.test",
            "accepted-password");
        var tenant = await app.SeedTenantAsync((inviter, TenantRole.Owner));

        await Should.ThrowAsync<DbUpdateException>(
            () => SeedInvitationAsync(
                tenant.Id,
                inviter.Id,
                new string('d', 64),
                "orphan-acceptance@example.test",
                $"missing-user-{Guid.NewGuid():N}"));

        var invitation = await SeedInvitationAsync(
            tenant.Id,
            inviter.Id,
            new string('e', 64),
            acceptedBy.Email,
            acceptedBy.Id);

        await Should.ThrowAsync<DbUpdateException>(
            () => app.WithDatabaseAsync(
                async db =>
                {
                    var storedUser = await db.Users.SingleAsync(
                        user => user.Id == acceptedBy.Id,
                        ct);
                    db.Users.Remove(storedUser);
                    await db.SaveChangesAsync(ct);
                    return true;
                }));

        var invitationStillExists = await app.WithDatabaseAsync(
            db => db.TenantInvitations.AnyAsync(
                candidate => candidate.Id == invitation.Id,
                ct));
        invitationStillExists.ShouldBeTrue();
    }

    [Fact]
    public async Task PostgreSql_DeletingTenantCascadesMembershipsAndInvitations()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await app.SeedUserAsync(
            "owner@example.test",
            "owner-password");
        var member = await app.SeedUserAsync(
            "member@example.test",
            "member-password");
        var tenant = await app.SeedTenantAsync(
            (owner, TenantRole.Owner),
            (member, TenantRole.Member));
        await SeedInvitationAsync(
            tenant.Id,
            owner.Id,
            new string('f', 64),
            "invitee@example.test");

        await app.WithDatabaseAsync(
            async db =>
            {
                var storedTenant = await db.Tenants.SingleAsync(
                    candidate => candidate.Id == tenant.Id,
                    ct);
                db.Tenants.Remove(storedTenant);
                await db.SaveChangesAsync(ct);
                return true;
            });

        await app.WithDatabaseAsync(
            async db =>
            {
                (await db.TenantMemberships.CountAsync(ct)).ShouldBe(0);
                (await db.TenantInvitations.CountAsync(ct)).ShouldBe(0);
                (await db.Users.CountAsync(ct)).ShouldBe(2);
                return true;
            });
    }

    [Fact]
    public async Task PostgreSql_DetectsConcurrentInvitationAcceptance()
    {
        var ct = TestContext.Current.CancellationToken;
        var inviter = await app.SeedUserAsync(
            "inviter@example.test",
            "inviter-password");
        var tenant = await app.SeedTenantAsync((inviter, TenantRole.Owner));
        var invitation = await SeedInvitationAsync(
            tenant.Id,
            inviter.Id,
            new string('c', 64),
            "invitee@example.test");

        using var firstScope = app.Services.CreateScope();
        using var secondScope = app.Services.CreateScope();
        var firstDb =
            firstScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var secondDb =
            secondScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var firstCopy = await firstDb.TenantInvitations.SingleAsync(
            candidate => candidate.Id == invitation.Id,
            ct);
        var secondCopy = await secondDb.TenantInvitations.SingleAsync(
            candidate => candidate.Id == invitation.Id,
            ct);

        firstCopy.AcceptedAt = DateTime.UtcNow.AddSeconds(-1);
        secondCopy.AcceptedAt = DateTime.UtcNow;
        await firstDb.SaveChangesAsync(ct);

        await Should.ThrowAsync<DbUpdateConcurrencyException>(
            () => secondDb.SaveChangesAsync(ct));
    }

    private WebApplication CreateProductionSeedApp(
        string adminEmail,
        string adminPassword,
        string? legacyAdminUsername = null)
    {
        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions
            {
                EnvironmentName = Environments.Production
            });
        var configurationValues = new Dictionary<string, string?>
        {
            ["Auth:AdminEmail"] = adminEmail,
            ["Auth:AdminPassword"] = adminPassword
        };
        if (legacyAdminUsername is not null)
        {
            configurationValues["Auth:LegacyAdminUsername"] =
                legacyAdminUsername;
        }

        builder.Configuration.AddInMemoryCollection(configurationValues);
        builder.Services.AddDbContext<ApplicationDbContext>(
            options => options.UseNpgsql(app.PostgreSqlConnectionString));
        return builder.Build();
    }

    private Task<TenantInvitation> SeedInvitationAsync(
        Guid tenantId,
        string invitedByUserId,
        string tokenHash,
        string email,
        string? acceptedByUserId = null)
    {
        return app.WithDatabaseAsync(
            async db =>
            {
                var invitation = new TenantInvitation
                {
                    TenantId = tenantId,
                    Email = email,
                    Role = TenantRole.Member,
                    TokenHash = tokenHash,
                    ExpiresAt = DateTime.UtcNow.AddHours(1),
                    InvitedByUserId = invitedByUserId,
                    AcceptedAt = acceptedByUserId is null
                        ? null
                        : DateTime.UtcNow,
                    AcceptedByUserId = acceptedByUserId
                };
                db.TenantInvitations.Add(invitation);
                await db.SaveChangesAsync(TestContext.Current.CancellationToken);
                return invitation;
            });
    }

    private sealed record RecordedLog(
        LogLevel Level,
        string Message,
        Exception? Exception);

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<RecordedLog> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(
                new RecordedLog(
                    logLevel,
                    formatter(state, exception),
                    exception));
        }
    }
}
