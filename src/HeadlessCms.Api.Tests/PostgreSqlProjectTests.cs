using System.Net;
using System.Net.Http.Json;
using FastEndpoints.Testing;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Endpoints.Projects;
using HeadlessCms.Api.Tenancy.Models;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace HeadlessCms.Api.Tests;

[Collection<TestAppCollection>]
public sealed class PostgreSqlProjectTests(TestApp app) : TestBase
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
            TestApp.ProjectsPath(tenant.Id),
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
            TestApp.ProjectsPath(tenant.Id),
            memberToken);
        memberListResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var memberGetResponse = await app.SendAsync(
            HttpMethod.Get,
            TestApp.ProjectPath(tenant.Id, project.Id),
            memberToken);
        memberGetResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var memberCreateResponse = await app.SendAsync(
            HttpMethod.Post,
            TestApp.ProjectsPath(tenant.Id),
            memberToken,
            new { name = "Forbidden create" });
        memberCreateResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var memberUpdateResponse = await app.SendAsync(
            HttpMethod.Put,
            TestApp.ProjectPath(tenant.Id, project.Id),
            memberToken,
            new { name = "Forbidden update" });
        memberUpdateResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var memberDeleteResponse = await app.SendAsync(
            HttpMethod.Delete,
            TestApp.ProjectPath(tenant.Id, project.Id),
            memberToken);
        memberDeleteResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var outsiderListResponse = await app.SendAsync(
            HttpMethod.Get,
            TestApp.ProjectsPath(tenant.Id),
            outsiderToken);
        outsiderListResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var outsiderGetResponse = await app.SendAsync(
            HttpMethod.Get,
            TestApp.ProjectPath(tenant.Id, project.Id),
            outsiderToken);
        outsiderGetResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var outsiderCreateResponse = await app.SendAsync(
            HttpMethod.Post,
            TestApp.ProjectsPath(tenant.Id),
            outsiderToken,
            new { name = "Forbidden create" });
        outsiderCreateResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var outsiderUpdateResponse = await app.SendAsync(
            HttpMethod.Put,
            TestApp.ProjectPath(tenant.Id, project.Id),
            outsiderToken,
            new { name = "Forbidden update" });
        outsiderUpdateResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var outsiderDeleteResponse = await app.SendAsync(
            HttpMethod.Delete,
            TestApp.ProjectPath(tenant.Id, project.Id),
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
            TestApp.ProjectPath(tenant.Id, secondTenantProject.Id),
            editorToken);
        crossTenantGetResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var crossTenantResponse = await app.SendAsync(
            HttpMethod.Put,
            TestApp.ProjectPath(tenant.Id, secondTenantProject.Id),
            editorToken,
            new { name = "Cross-tenant update" });
        crossTenantResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var crossTenantDeleteResponse = await app.SendAsync(
            HttpMethod.Delete,
            TestApp.ProjectPath(tenant.Id, secondTenantProject.Id),
            editorToken);
        crossTenantDeleteResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var editorUpdateResponse = await app.SendAsync(
            HttpMethod.Put,
            TestApp.ProjectPath(tenant.Id, project.Id),
            editorToken,
            new { name = "Editor update" });
        editorUpdateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var editorDeleteResponse = await app.SendAsync(
            HttpMethod.Delete,
            TestApp.ProjectPath(tenant.Id, project.Id),
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

}
