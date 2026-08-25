using FastEndpoints.Testing;
using HeadlessCms.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Shouldly;
using Xunit;

namespace HeadlessCms.Api.Tests;

[Collection<TestAppCollection>]
public sealed class MigrationRollbackTests(TestApp app) : TestBase
{
    protected override async ValueTask SetupAsync() => await app.ResetDatabaseAsync();

    [Fact]
    public async Task PostgreSql_AllMigrationsCanRollBackAndReapply()
    {
        var ct = TestContext.Current.CancellationToken;
        var migrationCount = await app.WithDatabaseAsync(
            async db => (await db.Database.GetAppliedMigrationsAsync(ct)).Count());
        migrationCount.ShouldBeGreaterThan(0);

        await app.WithDatabaseAsync(
            async db =>
            {
                var migrator = db.GetService<IMigrator>();
                await migrator.MigrateAsync(Migration.InitialDatabase, ct);

                var appliedAfterRollback =
                    await db.Database.GetAppliedMigrationsAsync(ct);
                appliedAfterRollback.ShouldBeEmpty();
                var usersTable = await db.Database.SqlQueryRaw<string?>(
                        "SELECT to_regclass('public.\"Users\"')::text AS \"Value\"")
                    .SingleAsync(ct);
                usersTable.ShouldBeNull();

                await migrator.MigrateAsync(cancellationToken: ct);
                return true;
            });

        var reappliedCount = await app.WithDatabaseAsync(
            async db => (await db.Database.GetAppliedMigrationsAsync(ct)).Count());
        reappliedCount.ShouldBe(migrationCount);

        var user = await app.SeedUserAsync("after-rollback", "password");
        user.Email.ShouldBe("after-rollback@example.test");
    }

    [Fact]
    public async Task SessionMigration_DropsLegacyTokensAndHasReversibleDown()
    {
        var ct = TestContext.Current.CancellationToken;

        await app.WithDatabaseAsync(async db =>
        {
            var migrations = db.Database.GetMigrations().ToList();
            migrations.Count.ShouldBeGreaterThan(1);
            (await TableAsync(db, "AuthSessions", ct)).ShouldBe("\"AuthSessions\"");
            (await TableAsync(db, "Tokens", ct)).ShouldBeNull();

            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(migrations[^2], ct);
            (await TableAsync(db, "AuthSessions", ct)).ShouldBeNull();
            (await TableAsync(db, "Tokens", ct)).ShouldBe("\"Tokens\"");

            await migrator.MigrateAsync(cancellationToken: ct);
            (await TableAsync(db, "AuthSessions", ct)).ShouldBe("\"AuthSessions\"");
            (await TableAsync(db, "Tokens", ct)).ShouldBeNull();
            return true;
        });
    }

    private static Task<string?> TableAsync(
        ApplicationDbContext db,
        string tableName,
        CancellationToken ct) =>
        db.Database.SqlQuery<string?>(
                $"SELECT to_regclass({$"public.\"{tableName}\""})::text AS \"Value\"")
            .SingleAsync(ct);
}
