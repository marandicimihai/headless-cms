using System.Net;
using System.Net.Http.Json;
using FastEndpoints.Testing;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Endpoints.Projects;
using HeadlessCms.Api.Tenancy.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Shouldly;
using Xunit;

namespace HeadlessCms.Api.Tests;

public sealed class PostgreSqlProjectTests(PostgreSqlApp app) : TestBase<PostgreSqlApp>
{
    protected override async ValueTask SetupAsync()
    {
        await app.ResetDatabaseAsync();
    }

    [Fact]
    public async Task PostgreSql_EnforcesTenantProjectRoleMatrix()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await app.SeedUserAsync("owner", "owner-password");
        var editor = await app.SeedUserAsync("editor", "editor-password");
        var member = await app.SeedUserAsync("member", "member-password");
        var platformAdmin = await app.SeedUserAsync(
            "platform-admin",
            "platform-admin-password",
            PlatformRole.PlatformAdmin);
        var tenant = await app.SeedTenantAsync(
            (owner, TenantRole.Owner),
            (editor, TenantRole.Editor),
            (member, TenantRole.Member));

        var ownerToken = await app.LoginAsync(owner.Email, "owner-password");
        var editorToken = await app.LoginAsync(editor.Email, "editor-password");
        var memberToken = await app.LoginAsync(member.Email, "member-password");
        var outsiderToken = await app.LoginAsync(
            platformAdmin.Email,
            "platform-admin-password");

        var createResponse = await app.SendAsync(
            HttpMethod.Post,
            ApiApp.ProjectsPath(tenant.Id),
            ownerToken,
            new { name = "PostgreSQL project" });
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var project = await createResponse.Content.ReadFromJsonAsync<CreateProjectResponse>(
            cancellationToken: ct);
        project.ShouldNotBeNull();

        var persistedTenantId = await app.WithDatabaseAsync(
            db => db.Projects
                .Where(candidate => candidate.Id == project.Id)
                .Select(candidate => candidate.TenantId)
                .SingleAsync(ct));
        persistedTenantId.ShouldBe(tenant.Id);

        var memberListResponse = await app.SendAsync(
            HttpMethod.Get,
            ApiApp.ProjectsPath(tenant.Id),
            memberToken);
        memberListResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var memberGetResponse = await app.SendAsync(
            HttpMethod.Get,
            ApiApp.ProjectPath(tenant.Id, project.Id),
            memberToken);
        memberGetResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var memberCreateResponse = await app.SendAsync(
            HttpMethod.Post,
            ApiApp.ProjectsPath(tenant.Id),
            memberToken,
            new { name = "Forbidden create" });
        memberCreateResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var memberUpdateResponse = await app.SendAsync(
            HttpMethod.Put,
            ApiApp.ProjectPath(tenant.Id, project.Id),
            memberToken,
            new { name = "Forbidden update" });
        memberUpdateResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var memberDeleteResponse = await app.SendAsync(
            HttpMethod.Delete,
            ApiApp.ProjectPath(tenant.Id, project.Id),
            memberToken);
        memberDeleteResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var outsiderListResponse = await app.SendAsync(
            HttpMethod.Get,
            ApiApp.ProjectsPath(tenant.Id),
            outsiderToken);
        outsiderListResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var outsiderGetResponse = await app.SendAsync(
            HttpMethod.Get,
            ApiApp.ProjectPath(tenant.Id, project.Id),
            outsiderToken);
        outsiderGetResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var outsiderCreateResponse = await app.SendAsync(
            HttpMethod.Post,
            ApiApp.ProjectsPath(tenant.Id),
            outsiderToken,
            new { name = "Forbidden create" });
        outsiderCreateResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var outsiderUpdateResponse = await app.SendAsync(
            HttpMethod.Put,
            ApiApp.ProjectPath(tenant.Id, project.Id),
            outsiderToken,
            new { name = "Forbidden update" });
        outsiderUpdateResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var outsiderDeleteResponse = await app.SendAsync(
            HttpMethod.Delete,
            ApiApp.ProjectPath(tenant.Id, project.Id),
            outsiderToken);
        outsiderDeleteResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var protectedProjectName = await app.WithDatabaseAsync(
            db => db.Projects
                .Where(candidate => candidate.Id == project.Id)
                .Select(candidate => candidate.Name)
                .SingleAsync(ct));
        protectedProjectName.ShouldBe("PostgreSQL project");

        var secondTenant = await app.SeedTenantAsync((editor, TenantRole.Editor));
        var secondTenantProject = await app.SeedProjectAsync(
            secondTenant.Id,
            "Other tenant project");
        var crossTenantGetResponse = await app.SendAsync(
            HttpMethod.Get,
            ApiApp.ProjectPath(tenant.Id, secondTenantProject.Id),
            editorToken);
        crossTenantGetResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var crossTenantResponse = await app.SendAsync(
            HttpMethod.Put,
            ApiApp.ProjectPath(tenant.Id, secondTenantProject.Id),
            editorToken,
            new { name = "Cross-tenant update" });
        crossTenantResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var crossTenantDeleteResponse = await app.SendAsync(
            HttpMethod.Delete,
            ApiApp.ProjectPath(tenant.Id, secondTenantProject.Id),
            editorToken);
        crossTenantDeleteResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var editorUpdateResponse = await app.SendAsync(
            HttpMethod.Put,
            ApiApp.ProjectPath(tenant.Id, project.Id),
            editorToken,
            new { name = "Editor update" });
        editorUpdateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var editorDeleteResponse = await app.SendAsync(
            HttpMethod.Delete,
            ApiApp.ProjectPath(tenant.Id, project.Id),
            editorToken);
        editorDeleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var remainingProject = await app.WithDatabaseAsync(
            db => db.Projects.AsNoTracking().SingleAsync(ct));
        remainingProject.Id.ShouldBe(secondTenantProject.Id);
        remainingProject.Name.ShouldBe("Other tenant project");
    }

    [Fact]
    public async Task PostgreSql_EnforcesTenantForeignKeyAndCascadeDelete()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await app.SeedUserAsync("owner", "owner-password");
        var tenant = await app.SeedTenantAsync((owner, TenantRole.Owner));
        await app.SeedProjectAsync(tenant.Id, "Cascade project");

        await Should.ThrowAsync<DbUpdateException>(
            () => app.WithDatabaseAsync(
                async db =>
                {
                    db.Projects.Add(
                        new Project
                        {
                            TenantId = Guid.NewGuid(),
                            Name = "Orphan project",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        });
                    await db.SaveChangesAsync(ct);
                    return true;
                }));

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

        var projectCount = await app.WithDatabaseAsync(db => db.Projects.CountAsync(ct));
        projectCount.ShouldBe(0);
    }

    [Fact]
    public async Task PostgreSql_MigratesLegacyUserOwnedProjectToTenantOwnership()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = Guid.NewGuid();
        var legacyOwnerId = $"legacy-owner-{Guid.NewGuid():N}";
        var username = $"legacy-{Guid.NewGuid():N}";
        var passwordHash = "legacy-password-hash";
        var projectName = "Legacy project";
        var createdAt = new DateTime(
            DateTime.UtcNow.AddDays(-1).Ticks / TimeSpan.TicksPerSecond
                * TimeSpan.TicksPerSecond,
            DateTimeKind.Utc);
        var updatedAt = createdAt.AddHours(2);

        await app.WithDatabaseAsync(
            async db =>
            {
                await db.Database.ExecuteSqlRawAsync(
                    """
                    DROP SCHEMA IF EXISTS public CASCADE;
                    CREATE SCHEMA public;
                    """,
                    ct);
                var migrator = db.GetService<IMigrator>();
                await migrator.MigrateAsync(
                    "20260729071632_AddTenantAuthorization",
                    ct);

                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    INSERT INTO "Users"
                        ("Id", "Username", "PasswordHash", "Email", "PlatformRole")
                    VALUES
                        ({legacyOwnerId}, {username}, {passwordHash}, {null}, {"User"});
                    """,
                    ct);

                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    INSERT INTO "Projects"
                        ("Id", "Name", "OwnerId", "CreatedAt", "UpdatedAt")
                    VALUES
                        ({projectId}, {projectName}, {legacyOwnerId}, {createdAt}, {updatedAt});
                    """,
                    ct);

                await migrator.MigrateAsync(
                    "20260729080035_MakeProjectsTenantOwned",
                    ct);
                return true;
            });

        await app.WithDatabaseAsync(
            async db =>
            {
                var migratedProject = await db.Projects
                    .AsNoTracking()
                    .SingleAsync(candidate => candidate.Id == projectId, ct);
                migratedProject.Name.ShouldBe(projectName);
                migratedProject.CreatedAt.ShouldBe(createdAt);
                migratedProject.UpdatedAt.ShouldBe(updatedAt);
                migratedProject.TenantId.ShouldNotBe(Guid.Empty);

                var tenant = await db.Tenants
                    .AsNoTracking()
                    .SingleAsync(candidate => candidate.Id == migratedProject.TenantId, ct);
                tenant.Name.ShouldBe(projectName);
                tenant.CreatedAt.ShouldBe(createdAt);

                var membership = await db.TenantMemberships
                    .AsNoTracking()
                    .SingleAsync(
                        candidate =>
                            candidate.TenantId == migratedProject.TenantId &&
                            candidate.UserId == legacyOwnerId,
                        ct);
                membership.Role.ShouldBe(TenantRole.Owner);
                membership.JoinedAt.ShouldBe(createdAt);

                var projectColumns = await db.Database.SqlQueryRaw<string>(
                        """
                        SELECT column_name AS "Value"
                        FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name = 'Projects'
                        """)
                    .ToListAsync(ct);
                projectColumns.ShouldContain("TenantId");
                projectColumns.ShouldNotContain("OwnerId");

                var tenantIdNullable = await db.Database.SqlQueryRaw<string>(
                        """
                        SELECT is_nullable AS "Value"
                        FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name = 'Projects'
                          AND column_name = 'TenantId'
                        """)
                    .SingleAsync(ct);
                tenantIdNullable.ShouldBe("NO");

                return true;
            });
    }
}
