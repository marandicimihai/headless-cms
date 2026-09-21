using FastEndpoints.Testing;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Endpoints.Auth;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;
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
    public async Task HealthEndpoints_AreAnonymousAndReportHealthy()
    {
        var ct = TestContext.Current.CancellationToken;

        var liveResponse = await app.HttpsClient.GetAsync("/health/live", ct);
        var readyResponse = await app.HttpsClient.GetAsync("/health/ready", ct);

        liveResponse.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        readyResponse.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
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

    [Fact]
    public async Task Initialize_SerializesConcurrentRuns_AndPreservesExistingPassword()
    {
        await using var first = CreateProductionSeedApp("bootstrap@example.test", "initial-password");
        await using var second = CreateProductionSeedApp("bootstrap@example.test", "initial-password");
        await Task.WhenAll(
            DatabaseInitializer.InitializeAsync(first, app.PostgreSqlConnectionString),
            DatabaseInitializer.InitializeAsync(second, app.PostgreSqlConnectionString));
        var original = await app.WithDatabaseAsync(db => db.Users.AsNoTracking().SingleAsync());
        await using var repeated = CreateProductionSeedApp("bootstrap@example.test", "different-password");
        await DatabaseInitializer.InitializeAsync(repeated, app.PostgreSqlConnectionString);
        var current = await app.WithDatabaseAsync(db => db.Users.AsNoTracking().SingleAsync());
        current.Id.ShouldBe(original.Id);
        current.PasswordHash.ShouldBe(original.PasswordHash);
        current.PlatformRole.ShouldBe(PlatformRole.PlatformAdmin);
    }

    [Fact]
    public async Task InvitationLinks_AreReturnedOnlyOnIssue_AndRegenerationInvalidatesOldLink()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await app.SeedUserAsync("link-owner", "owner-password");
        var member = await app.SeedUserAsync("link-member", "member-password");
        var workspace = await app.SeedWorkspaceAsync((owner, WorkspaceRole.Owner), (member, WorkspaceRole.Member));
        var cookie = await app.LoginAsync(owner.Email, "owner-password");
        var path = $"/api/workspaces/{workspace.Id}/invitations";
        using var created = await app.SendAsync(HttpMethod.Post, path, cookie,
            new { email = "invitee@example.test", role = "member" });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        created.Headers.CacheControl!.NoStore.ShouldBeTrue();
        var first = await created.Content.ReadFromJsonAsync<JsonElement>(ct);
        var originalUrl = first.GetProperty("invitationUrl").GetString()!;
        originalUrl.ShouldStartWith("http://localhost:3000/auth/invitations/accept?token=");
        var originalToken = new Uri(originalUrl).Query.Split("=", 2)[1];
        var id = first.GetProperty("id").GetGuid();
        using var listed = await app.SendAsync(HttpMethod.Get, path, cookie);
        (await listed.Content.ReadAsStringAsync(ct)).ShouldNotContain("invitationUrl");
        var memberCookie = await app.LoginAsync(member.Email, "member-password");
        using var forbidden = await app.SendAsync(HttpMethod.Post, $"{path}/{id}/resend", memberCookie);
        forbidden.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var regenerated = await app.SendAsync(HttpMethod.Post, $"{path}/{id}/resend", cookie);
        regenerated.StatusCode.ShouldBe(HttpStatusCode.OK);
        regenerated.Headers.CacheControl!.NoStore.ShouldBeTrue();
        var second = await regenerated.Content.ReadFromJsonAsync<JsonElement>(ct);
        second.GetProperty("invitationUrl").GetString().ShouldNotBe(originalUrl);
        using var expired = await app.HttpsClient.PostAsJsonAsync("/api/auth/invitations/preview",
            new { token = originalToken }, ct);
        expired.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var nextToken = new Uri(second.GetProperty("invitationUrl").GetString()!).Query.Split("=", 2)[1];
        using var preview = await app.HttpsClient.PostAsJsonAsync("/api/auth/invitations/preview",
            new { token = nextToken }, ct);
        preview.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SeedPlatformAdminUser_InProduction_PromotesExistingUser()
    {
        var ct = TestContext.Current.CancellationToken;
        var existing = await app.SeedUserAsync(
            "existing@example.test",
            "existing-password");
        var originalPasswordHash = existing.PasswordHash;
        await using var productionApp = CreateProductionSeedApp(
            " Existing@Example.Test ",
            "unused-admin-password");

        await productionApp.SeedPlatformAdminUser();

        var users = await app.WithDatabaseAsync(
            db => db.Users.AsNoTracking().ToListAsync(ct));
        var promoted = users.Single();
        promoted.Id.ShouldBe(existing.Id);
        promoted.Email.ShouldBe("existing@example.test");
        promoted.PlatformRole.ShouldBe(PlatformRole.PlatformAdmin);
        promoted.PasswordHash.ShouldBe(originalPasswordHash);
    }

    [Theory]
    [InlineData(
        "https://cms.example.test",
        "https://cms.example.test/auth/invitations/accept")]
    public async Task LoggingInvitationEmailSender_LogsEscapedInvitationUrl(
        string? configuredBaseUrl,
        string expectedBaseUrl)
    {
        var logger = new RecordingLogger<LoggingInvitationEmailSender>();
        var configurationValues = new Dictionary<string, string?>();
        if (configuredBaseUrl is not null)
            configurationValues["Frontend:BaseUrl"] = configuredBaseUrl;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();
        var sender = new LoggingInvitationEmailSender(logger, configuration);

        await sender.SendAsync(
            "invitee@example.test",
            "Workspace A",
            WorkspaceRole.Editor,
            "a+b/c?=",
            TestContext.Current.CancellationToken);

        var entry = logger.Entries.Single();
        entry.Level.ShouldBe(LogLevel.Information);
        entry.Exception.ShouldBeNull();
        entry.Message.ShouldBe(
            "Development invitation for invitee@example.test to Workspace A as Editor: " +
            $"{expectedBaseUrl}?token=a%2Bb%2Fc%3F%3D");
    }

    [Fact]
    public async Task UnconfiguredInvitationEmailSender_RejectsProductionDelivery()
    {
        var sender = new UnconfiguredInvitationEmailSender();

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await sender.SendAsync(
                "invitee@example.test",
                "Workspace A",
                WorkspaceRole.Member,
                "token",
                TestContext.Current.CancellationToken));

        exception.Message.ShouldBe(
            "No production invitation email sender has been configured.");
    }

    [Theory]
    [InlineData(
        null,
        "Required configuration 'Workspaces:InvitationExpirationHours' is missing.")]
    [InlineData(
        "0",
        "Workspaces invitation expiration must be greater than zero.")]
    [InlineData(
        "-1",
        "Workspaces invitation expiration must be greater than zero.")]
    public async Task WorkspaceInvitationService_RejectsMissingOrInvalidExpiration(
        string? configuredHours,
        string expectedMessage)
    {
        var configurationValues = new Dictionary<string, string?>();
        if (configuredHours is not null)
        {
            configurationValues["Workspaces:InvitationExpirationHours"] =
                configuredHours;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();
        using var scope = app.Services.CreateScope();
        var service = new WorkspaceInvitationService(
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(),
            configuration,
            scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>(),
            scope.ServiceProvider.GetRequiredService<IInvitationEmailSender>(),
            scope.ServiceProvider.GetRequiredService<WorkspaceAccessService>());
        var pendingInvitation = new WorkspaceInvitation
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
    public async Task PostgreSql_AllowsOnlyOneOwnerPerWorkspace()
    {
        var ct = TestContext.Current.CancellationToken;
        var firstOwner = await app.SeedUserAsync(
            "first-owner@example.test",
            "first-password");
        var secondOwner = await app.SeedUserAsync(
            "second-owner@example.test",
            "second-password");

        await Should.ThrowAsync<DbUpdateException>(
            () => app.SeedWorkspaceAsync(
                (firstOwner, WorkspaceRole.Owner),
                (secondOwner, WorkspaceRole.Owner)));

        var workspaceCount = await app.WithDatabaseAsync(
            db => db.Workspaces.CountAsync(ct));
        workspaceCount.ShouldBe(0);
    }

    [Fact]
    public async Task PostgreSql_EnforcesWorkspaceMembershipCompositeKey()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = await app.SeedUserAsync(
            "member@example.test",
            "member-password");
        var workspace = await app.SeedWorkspaceAsync((user, WorkspaceRole.Member));

        await Should.ThrowAsync<DbUpdateException>(
            () => app.WithDatabaseAsync(
                async db =>
                {
                    db.WorkspaceMemberships.Add(
                        new WorkspaceMembership
                        {
                            WorkspaceId = workspace.Id,
                            UserId = user.Id,
                            Role = WorkspaceRole.Editor
                        });
                    await db.SaveChangesAsync(ct);
                    return true;
                }));

        var membership = await app.WithDatabaseAsync(
            db => db.WorkspaceMemberships.AsNoTracking().SingleAsync(ct));
        membership.Role.ShouldBe(WorkspaceRole.Member);
    }

    [Fact]
    public async Task PostgreSql_EnforcesInvitationRelationshipsAndUniqueTokenHash()
    {
        var ct = TestContext.Current.CancellationToken;
        var inviter = await app.SeedUserAsync(
            "inviter@example.test",
            "inviter-password");
        var workspace = await app.SeedWorkspaceAsync((inviter, WorkspaceRole.Owner));

        await Should.ThrowAsync<DbUpdateException>(
            () => SeedInvitationAsync(
                workspace.Id,
                $"missing-user-{Guid.NewGuid():N}",
                new string('a', 64),
                "orphan@example.test"));

        var invitation = await SeedInvitationAsync(
            workspace.Id,
            inviter.Id,
            new string('b', 64),
            "first@example.test");

        await Should.ThrowAsync<DbUpdateException>(
            () => SeedInvitationAsync(
                workspace.Id,
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
            db => db.WorkspaceInvitations.AnyAsync(
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
        var workspace = await app.SeedWorkspaceAsync((inviter, WorkspaceRole.Owner));

        await Should.ThrowAsync<DbUpdateException>(
            () => SeedInvitationAsync(
                workspace.Id,
                inviter.Id,
                new string('d', 64),
                "orphan-acceptance@example.test",
                $"missing-user-{Guid.NewGuid():N}"));

        var invitation = await SeedInvitationAsync(
            workspace.Id,
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
            db => db.WorkspaceInvitations.AnyAsync(
                candidate => candidate.Id == invitation.Id,
                ct));
        invitationStillExists.ShouldBeTrue();
    }

    [Fact]
    public async Task PostgreSql_DeletingWorkspaceCascadesMembershipsAndInvitations()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await app.SeedUserAsync(
            "owner@example.test",
            "owner-password");
        var member = await app.SeedUserAsync(
            "member@example.test",
            "member-password");
        var workspace = await app.SeedWorkspaceAsync(
            (owner, WorkspaceRole.Owner),
            (member, WorkspaceRole.Member));
        await SeedInvitationAsync(
            workspace.Id,
            owner.Id,
            new string('f', 64),
            "invitee@example.test");

        await app.WithDatabaseAsync(
            async db =>
            {
                var storedWorkspace = await db.Workspaces.SingleAsync(
                    candidate => candidate.Id == workspace.Id,
                    ct);
                db.Workspaces.Remove(storedWorkspace);
                await db.SaveChangesAsync(ct);
                return true;
            });

        await app.WithDatabaseAsync(
            async db =>
            {
                (await db.WorkspaceMemberships.CountAsync(ct)).ShouldBe(0);
                (await db.WorkspaceInvitations.CountAsync(ct)).ShouldBe(0);
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
        var workspace = await app.SeedWorkspaceAsync((inviter, WorkspaceRole.Owner));
        var invitation = await SeedInvitationAsync(
            workspace.Id,
            inviter.Id,
            new string('c', 64),
            "invitee@example.test");

        using var firstScope = app.Services.CreateScope();
        using var secondScope = app.Services.CreateScope();
        var firstDb =
            firstScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var secondDb =
            secondScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var firstCopy = await firstDb.WorkspaceInvitations.SingleAsync(
            candidate => candidate.Id == invitation.Id,
            ct);
        var secondCopy = await secondDb.WorkspaceInvitations.SingleAsync(
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
        string adminPassword)
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
        builder.Configuration.AddInMemoryCollection(configurationValues);
        builder.Services.AddDbContext<ApplicationDbContext>(
            options => options.UseNpgsql(app.PostgreSqlConnectionString));
        return builder.Build();
    }

    private Task<WorkspaceInvitation> SeedInvitationAsync(
        Guid workspaceId,
        string invitedByUserId,
        string tokenHash,
        string email,
        string? acceptedByUserId = null)
    {
        return app.WithDatabaseAsync(
            async db =>
            {
                var invitation = new WorkspaceInvitation
                {
                    WorkspaceId = workspaceId,
                    Email = email,
                    Role = WorkspaceRole.Member,
                    TokenHash = tokenHash,
                    ExpiresAt = DateTime.UtcNow.AddHours(1),
                    InvitedByUserId = invitedByUserId,
                    AcceptedAt = acceptedByUserId is null
                        ? null
                        : DateTime.UtcNow,
                    AcceptedByUserId = acceptedByUserId
                };
                db.WorkspaceInvitations.Add(invitation);
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
