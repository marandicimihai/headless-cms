using System.Net;
using System.Net.Http.Json;
using FastEndpoints.Testing;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Endpoints.Projects;
using HeadlessCms.Api.Workspaces.Models;
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
    public async Task PostgreSql_EnforcesWorkspaceProjectRoleMatrix()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await app.SeedUserAsync("owner", "owner-password");
        var editor = await app.SeedUserAsync("editor", "editor-password");
        var member = await app.SeedUserAsync("member", "member-password");
        var platformAdmin = await app.SeedUserAsync(
            "platform-admin",
            "platform-admin-password",
            PlatformRole.PlatformAdmin);
        var workspace = await app.SeedWorkspaceAsync(
            (owner, WorkspaceRole.Owner),
            (editor, WorkspaceRole.Editor),
            (member, WorkspaceRole.Member));

        var ownerToken = await app.LoginAsync(owner.Email, "owner-password");
        var editorToken = await app.LoginAsync(editor.Email, "editor-password");
        var memberToken = await app.LoginAsync(member.Email, "member-password");
        var outsiderToken = await app.LoginAsync(
            platformAdmin.Email,
            "platform-admin-password");

        var createResponse = await app.SendAsync(
            HttpMethod.Post,
            TestApp.ProjectsPath(workspace.Id),
            ownerToken,
            new { name = "PostgreSQL project" });
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var project = await createResponse.Content.ReadFromJsonAsync<CreateProjectResponse>(
            cancellationToken: ct);
        project.ShouldNotBeNull();

        var persistedWorkspaceId = await app.WithDatabaseAsync(
            db => db.Projects
                .Where(candidate => candidate.Id == project.Id)
                .Select(candidate => candidate.WorkspaceId)
                .SingleAsync(ct));
        persistedWorkspaceId.ShouldBe(workspace.Id);

        var memberListResponse = await app.SendAsync(
            HttpMethod.Get,
            TestApp.ProjectsPath(workspace.Id),
            memberToken);
        memberListResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var memberGetResponse = await app.SendAsync(
            HttpMethod.Get,
            TestApp.ProjectPath(workspace.Id, project.Id),
            memberToken);
        memberGetResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var memberCreateResponse = await app.SendAsync(
            HttpMethod.Post,
            TestApp.ProjectsPath(workspace.Id),
            memberToken,
            new { name = "Forbidden create" });
        memberCreateResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var memberUpdateResponse = await app.SendAsync(
            HttpMethod.Put,
            TestApp.ProjectPath(workspace.Id, project.Id),
            memberToken,
            new { name = "Forbidden update" });
        memberUpdateResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var memberDeleteResponse = await app.SendAsync(
            HttpMethod.Delete,
            TestApp.ProjectPath(workspace.Id, project.Id),
            memberToken);
        memberDeleteResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var outsiderListResponse = await app.SendAsync(
            HttpMethod.Get,
            TestApp.ProjectsPath(workspace.Id),
            outsiderToken);
        outsiderListResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var outsiderGetResponse = await app.SendAsync(
            HttpMethod.Get,
            TestApp.ProjectPath(workspace.Id, project.Id),
            outsiderToken);
        outsiderGetResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var outsiderCreateResponse = await app.SendAsync(
            HttpMethod.Post,
            TestApp.ProjectsPath(workspace.Id),
            outsiderToken,
            new { name = "Forbidden create" });
        outsiderCreateResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var outsiderUpdateResponse = await app.SendAsync(
            HttpMethod.Put,
            TestApp.ProjectPath(workspace.Id, project.Id),
            outsiderToken,
            new { name = "Forbidden update" });
        outsiderUpdateResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var outsiderDeleteResponse = await app.SendAsync(
            HttpMethod.Delete,
            TestApp.ProjectPath(workspace.Id, project.Id),
            outsiderToken);
        outsiderDeleteResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var protectedProjectName = await app.WithDatabaseAsync(
            db => db.Projects
                .Where(candidate => candidate.Id == project.Id)
                .Select(candidate => candidate.Name)
                .SingleAsync(ct));
        protectedProjectName.ShouldBe("PostgreSQL project");

        var secondWorkspace = await app.SeedWorkspaceAsync((editor, WorkspaceRole.Editor));
        var secondWorkspaceProject = await app.SeedProjectAsync(
            secondWorkspace.Id,
            "Other workspace project");
        var crossWorkspaceGetResponse = await app.SendAsync(
            HttpMethod.Get,
            TestApp.ProjectPath(workspace.Id, secondWorkspaceProject.Id),
            editorToken);
        crossWorkspaceGetResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var crossWorkspaceResponse = await app.SendAsync(
            HttpMethod.Put,
            TestApp.ProjectPath(workspace.Id, secondWorkspaceProject.Id),
            editorToken,
            new { name = "Cross-workspace update" });
        crossWorkspaceResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var crossWorkspaceDeleteResponse = await app.SendAsync(
            HttpMethod.Delete,
            TestApp.ProjectPath(workspace.Id, secondWorkspaceProject.Id),
            editorToken);
        crossWorkspaceDeleteResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var editorUpdateResponse = await app.SendAsync(
            HttpMethod.Put,
            TestApp.ProjectPath(workspace.Id, project.Id),
            editorToken,
            new { name = "Editor update" });
        editorUpdateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var editorDeleteResponse = await app.SendAsync(
            HttpMethod.Delete,
            TestApp.ProjectPath(workspace.Id, project.Id),
            editorToken);
        editorDeleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var remainingProject = await app.WithDatabaseAsync(
            db => db.Projects.AsNoTracking().SingleAsync(ct));
        remainingProject.Id.ShouldBe(secondWorkspaceProject.Id);
        remainingProject.Name.ShouldBe("Other workspace project");
    }

    [Fact]
    public async Task PostgreSql_EnforcesWorkspaceForeignKeyAndCascadeDelete()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await app.SeedUserAsync("owner", "owner-password");
        var workspace = await app.SeedWorkspaceAsync((owner, WorkspaceRole.Owner));
        await app.SeedProjectAsync(workspace.Id, "Cascade project");

        await Should.ThrowAsync<DbUpdateException>(
            () => app.WithDatabaseAsync(
                async db =>
                {
                    db.Projects.Add(
                        new Project
                        {
                            WorkspaceId = Guid.NewGuid(),
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
                var storedWorkspace = await db.Workspaces.SingleAsync(
                    candidate => candidate.Id == workspace.Id,
                    ct);
                db.Workspaces.Remove(storedWorkspace);
                await db.SaveChangesAsync(ct);
                return true;
            });

        var projectCount = await app.WithDatabaseAsync(db => db.Projects.CountAsync(ct));
        projectCount.ShouldBe(0);
    }

}
