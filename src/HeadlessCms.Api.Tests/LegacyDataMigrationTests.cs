using FastEndpoints.Testing;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Shouldly;
using Xunit;

namespace HeadlessCms.Api.Tests;

[Collection<TestAppCollection>]
public sealed class LegacyDataMigrationTests(TestApp app) : TestBase
{
    private const string AddProjectsMigration =
        "20260729071515_AddProjects";
    private const string AddTenantAuthorizationMigration =
        "20260729071632_AddTenantAuthorization";
    private const string ImplementTenantLifecycleMigration =
        "20260729080217_ImplementTenantLifecycle";

    protected override async ValueTask SetupAsync()
    {
        await app.ResetDatabaseAsync();
    }

    [Fact]
    public async Task AddTenantAuthorization_BackfillsExistingUsersAsRegularUsers()
    {
        var ct = TestContext.Current.CancellationToken;
        var legacyUserId = $"legacy-user-{Guid.NewGuid():N}";

        await app.WithDatabaseAsync(
            async db =>
            {
                await RecreateSchemaAtAsync(
                    db,
                    AddProjectsMigration,
                    ct);
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    INSERT INTO "Users" ("Id", "Username", "PasswordHash")
                    VALUES ({legacyUserId}, {"LegacyUser"}, {"legacy-password-hash"});
                    """,
                    ct);

                await db.GetService<IMigrator>().MigrateAsync(
                    AddTenantAuthorizationMigration,
                    ct);

                var platformRole = await db.Database.SqlQueryRaw<string>(
                        """
                        SELECT "PlatformRole" AS "Value"
                        FROM "Users"
                        WHERE "Id" = {0}
                        """,
                        legacyUserId)
                    .SingleAsync(ct);
                platformRole.ShouldBe(nameof(PlatformRole.User));

                var platformRoleNullable = await db.Database.SqlQueryRaw<string>(
                        """
                        SELECT is_nullable AS "Value"
                        FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name = 'Users'
                          AND column_name = 'PlatformRole'
                        """)
                    .SingleAsync(ct);
                platformRoleNullable.ShouldBe("NO");
                return true;
            });
    }

    [Fact]
    public async Task ImplementTenantLifecycle_BackfillsEmailsAndInvitationCreatedAt()
    {
        var ct = TestContext.Current.CancellationToken;
        var emailUsernameUserId = $"legacy-email-{Guid.NewGuid():N}";
        var plainUsernameUserId = $"legacy-plain-{Guid.NewGuid():N}";
        var tenantId = Guid.NewGuid();
        var invitationId = Guid.NewGuid();
        var tenantCreatedAt = DateTime.UtcNow.AddDays(-2);
        var invitationExpiresAt = DateTime.UtcNow.AddDays(1);

        await app.WithDatabaseAsync(
            async db =>
            {
                await RecreateSchemaAtAsync(
                    db,
                    AddTenantAuthorizationMigration,
                    ct);
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    INSERT INTO "Users"
                        ("Id", "Username", "PasswordHash", "Email", "PlatformRole")
                    VALUES
                        (
                            {emailUsernameUserId},
                            {"Legacy.Email@Example.COM"},
                            {"legacy-password-hash"},
                            {(string?)null},
                            {"User"}
                        ),
                        (
                            {plainUsernameUserId},
                            {"LegacyOwner"},
                            {"legacy-password-hash"},
                            {(string?)null},
                            {"User"}
                        );
                    """,
                    ct);
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    INSERT INTO "Tenants" ("Id", "Name", "CreatedAt")
                    VALUES ({tenantId}, {"Legacy tenant"}, {tenantCreatedAt});
                    """,
                    ct);
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    INSERT INTO "TenantInvitations"
                        (
                            "Id",
                            "TenantId",
                            "Email",
                            "Role",
                            "TokenHash",
                            "ExpiresAt",
                            "AcceptedAt",
                            "InvitedByUserId",
                            "AcceptedByUserId"
                        )
                    VALUES
                        (
                            {invitationId},
                            {tenantId},
                            {"invitee@example.test"},
                            {"Member"},
                            {new string('a', 64)},
                            {invitationExpiresAt},
                            {(DateTime?)null},
                            {plainUsernameUserId},
                            {(string?)null}
                        );
                    """,
                    ct);

                var beforeMigration = DateTime.UtcNow.AddSeconds(-1);
                await db.GetService<IMigrator>().MigrateAsync(
                    ImplementTenantLifecycleMigration,
                    ct);
                var afterMigration = DateTime.UtcNow.AddSeconds(1);

                var migratedUsers = await db.Users
                    .AsNoTracking()
                    .Where(
                        user =>
                            user.Id == emailUsernameUserId ||
                            user.Id == plainUsernameUserId)
                    .ToDictionaryAsync(user => user.Id, ct);
                migratedUsers[emailUsernameUserId].Email.ShouldBe(
                    "legacy.email@example.com");
                migratedUsers[plainUsernameUserId].Email.ShouldBe(
                    "legacyowner@legacy.invalid");

                var migratedInvitation = await db.TenantInvitations
                    .AsNoTracking()
                    .SingleAsync(
                        invitation => invitation.Id == invitationId,
                        ct);
                (migratedInvitation.CreatedAt >= beforeMigration).ShouldBeTrue();
                (migratedInvitation.CreatedAt <= afterMigration).ShouldBeTrue();

                var userColumns = await db.Database.SqlQueryRaw<string>(
                        """
                        SELECT column_name AS "Value"
                        FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name = 'Users'
                        """)
                    .ToListAsync(ct);
                userColumns.ShouldNotContain("Username");

                var createdAtNullable = await db.Database.SqlQueryRaw<string>(
                        """
                        SELECT is_nullable AS "Value"
                        FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name = 'TenantInvitations'
                          AND column_name = 'CreatedAt'
                        """)
                    .SingleAsync(ct);
                createdAtNullable.ShouldBe("NO");
                return true;
            });
    }

    private static async Task RecreateSchemaAtAsync(
        ApplicationDbContext db,
        string migration,
        CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            DROP SCHEMA IF EXISTS public CASCADE;
            CREATE SCHEMA public;
            """,
            ct);
        db.ChangeTracker.Clear();
        await db.GetService<IMigrator>().MigrateAsync(migration, ct);
    }
}
