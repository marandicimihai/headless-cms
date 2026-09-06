using System.Net;
using System.Net.Http.Json;
using FastEndpoints;
using FastEndpoints.Security;
using FastEndpoints.Testing;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Endpoints.Auth;
using HeadlessCms.Api.Endpoints.Projects;
using HeadlessCms.Api.Workspaces.Models;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace HeadlessCms.Api.Tests;

[Collection<ProjectEndpointCollection>]
public sealed class ProjectEndpointTests(ProjectEndpointTestApp app) : TestBase
{
    private string databaseName = null!;
    protected override async ValueTask SetupAsync()
    {
        databaseName = app.BeginTestDatabase();
        await app.InitializeDatabaseAsync();
    }

    protected override async ValueTask TearDownAsync() =>
        await app.CleanupDatabaseAsync(databaseName);

    [Fact]
    public async Task Owner_CanCreateReadUpdateAndDeleteWorkspaceProject()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await app.SeedUserAsync("owner", "owner-password");
        var workspace = await SeedWorkspaceAsync((owner, WorkspaceRole.Owner));
        var accessToken = await LoginAsync("owner", "owner-password");

        var createResponse = await SendAsync(
            HttpMethod.Post,
            ProjectsPath(workspace.Id),
            accessToken,
            new { name = "Website" });

        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateProjectResponse>(
            cancellationToken: ct);
        created.ShouldNotBeNull();
        created.WorkspaceId.ShouldBe(workspace.Id);
        created.Name.ShouldBe("Website");
        created.CreatedAt.ShouldBe(created.UpdatedAt);
        createResponse.Headers.Location.ShouldNotBeNull();
        createResponse.Headers.Location!.ToString().ShouldEndWith(
            $"{ProjectsPath(workspace.Id)}/{created.Id}");

        var storedWorkspaceId = await app.WithDatabaseAsync(
            db => db.Projects
                .Where(project => project.Id == created.Id)
                .Select(project => project.WorkspaceId)
                .SingleAsync(ct));
        storedWorkspaceId.ShouldBe(workspace.Id);

        var listResponse = await SendAsync(
            HttpMethod.Get,
            ProjectsPath(workspace.Id),
            accessToken);
        listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var projects = await listResponse.Content.ReadFromJsonAsync<List<ListProjectsItemResponse>>(
            cancellationToken: ct);
        projects.ShouldNotBeNull();
        projects.Select(project => project.Id).ShouldBe([created.Id]);

        var getResponse = await SendAsync(
            HttpMethod.Get,
            ProjectPath(workspace.Id, created.Id),
            accessToken);
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var updateResponse = await SendAsync(
            HttpMethod.Put,
            ProjectPath(workspace.Id, created.Id),
            accessToken,
            new { name = "Updated website" });
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<UpdateProjectResponse>(
            cancellationToken: ct);
        updated.ShouldNotBeNull();
        updated.Name.ShouldBe("Updated website");
        updated.UpdatedAt.ShouldBeGreaterThanOrEqualTo(created.UpdatedAt);

        var deleteResponse = await SendAsync(
            HttpMethod.Delete,
            ProjectPath(workspace.Id, created.Id),
            accessToken);
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var projectCount = await app.WithDatabaseAsync(db => db.Projects.CountAsync(ct));
        projectCount.ShouldBe(0);
    }

    [Fact]
    public async Task Editor_CanCreateUpdateAndDeleteWorkspaceProject()
    {
        var ct = TestContext.Current.CancellationToken;
        var editor = await app.SeedUserAsync("editor", "editor-password");
        var workspace = await SeedWorkspaceAsync((editor, WorkspaceRole.Editor));
        var accessToken = await LoginAsync("editor", "editor-password");

        var createResponse = await SendAsync(
            HttpMethod.Post,
            ProjectsPath(workspace.Id),
            accessToken,
            new { name = "Editor project" });
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var project = await createResponse.Content.ReadFromJsonAsync<CreateProjectResponse>(
            cancellationToken: ct);
        project.ShouldNotBeNull();

        var updateResponse = await SendAsync(
            HttpMethod.Put,
            ProjectPath(workspace.Id, project.Id),
            accessToken,
            new { name = "Edited project" });
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var deleteResponse = await SendAsync(
            HttpMethod.Delete,
            ProjectPath(workspace.Id, project.Id),
            accessToken);
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Member_CanReadWorkspaceProjectsButCannotWrite()
    {
        var ct = TestContext.Current.CancellationToken;
        var member = await app.SeedUserAsync("member", "member-password");
        var workspace = await SeedWorkspaceAsync((member, WorkspaceRole.Member));
        var project = await SeedProjectAsync(workspace.Id, "Read-only project");
        var accessToken = await LoginAsync("member", "member-password");

        var listResponse = await SendAsync(
            HttpMethod.Get,
            ProjectsPath(workspace.Id),
            accessToken);
        listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var projects = await listResponse.Content.ReadFromJsonAsync<List<ListProjectsItemResponse>>(
            cancellationToken: ct);
        projects.ShouldNotBeNull();
        projects.Select(candidate => candidate.Id).ShouldBe([project.Id]);

        var getResponse = await SendAsync(
            HttpMethod.Get,
            ProjectPath(workspace.Id, project.Id),
            accessToken);
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var createResponse = await SendAsync(
            HttpMethod.Post,
            ProjectsPath(workspace.Id),
            accessToken,
            new { name = "Forbidden project" });
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var updateResponse = await SendAsync(
            HttpMethod.Put,
            ProjectPath(workspace.Id, project.Id),
            accessToken,
            new { name = "Forbidden update" });
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var deleteResponse = await SendAsync(
            HttpMethod.Delete,
            ProjectPath(workspace.Id, project.Id),
            accessToken);
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var storedProject = await app.WithDatabaseAsync(
            db => db.Projects.AsNoTracking().SingleAsync(ct));
        storedProject.Name.ShouldBe("Read-only project");
    }

    [Fact]
    public async Task NonMember_CannotDiscoverOrMutateWorkspaceProjects()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await app.SeedUserAsync("owner", "owner-password");
        var outsider = await app.SeedUserAsync("outsider", "outsider-password");
        var workspace = await SeedWorkspaceAsync((owner, WorkspaceRole.Owner));
        var project = await SeedProjectAsync(workspace.Id, "Private project");
        var accessToken = await LoginAsync(outsider.Email, "outsider-password");

        var listResponse = await SendAsync(
            HttpMethod.Get,
            ProjectsPath(workspace.Id),
            accessToken);
        listResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var getResponse = await SendAsync(
            HttpMethod.Get,
            ProjectPath(workspace.Id, project.Id),
            accessToken);
        getResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var createResponse = await SendAsync(
            HttpMethod.Post,
            ProjectsPath(workspace.Id),
            accessToken,
            new { name = "Stolen project" });
        createResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var updateResponse = await SendAsync(
            HttpMethod.Put,
            ProjectPath(workspace.Id, project.Id),
            accessToken,
            new { name = "Stolen project" });
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var deleteResponse = await SendAsync(
            HttpMethod.Delete,
            ProjectPath(workspace.Id, project.Id),
            accessToken);
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var storedProject = await app.WithDatabaseAsync(
            db => db.Projects.AsNoTracking().SingleAsync(ct));
        storedProject.Name.ShouldBe("Private project");
        storedProject.WorkspaceId.ShouldBe(workspace.Id);
    }

    [Fact]
    public async Task ProjectId_CannotBeUsedThroughAnotherWorkspaceRoute()
    {
        var editor = await app.SeedUserAsync("editor", "editor-password");
        var firstWorkspace = await SeedWorkspaceAsync((editor, WorkspaceRole.Editor));
        var secondWorkspace = await SeedWorkspaceAsync((editor, WorkspaceRole.Editor));
        var secondWorkspaceProject = await SeedProjectAsync(secondWorkspace.Id, "Other workspace project");
        var accessToken = await LoginAsync("editor", "editor-password");

        var getResponse = await SendAsync(
            HttpMethod.Get,
            ProjectPath(firstWorkspace.Id, secondWorkspaceProject.Id),
            accessToken);
        getResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var updateResponse = await SendAsync(
            HttpMethod.Put,
            ProjectPath(firstWorkspace.Id, secondWorkspaceProject.Id),
            accessToken,
            new { name = "Cross-workspace update" });
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var deleteResponse = await SendAsync(
            HttpMethod.Delete,
            ProjectPath(firstWorkspace.Id, secondWorkspaceProject.Id),
            accessToken);
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("GET", false, false)]
    [InlineData("GET", true, false)]
    [InlineData("POST", false, true)]
    [InlineData("PUT", true, true)]
    [InlineData("DELETE", true, false)]
    public async Task ProjectEndpoints_RejectAnonymousRequests(
        string method,
        bool includeProjectId,
        bool includeBody)
    {
        var workspaceId = Guid.NewGuid();
        var path = includeProjectId
            ? ProjectPath(workspaceId, Guid.NewGuid())
            : ProjectsPath(workspaceId);
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (includeBody)
            request.Content = JsonContent.Create(new { name = "Private project" });

        using var response = await app.HttpsClient.SendAsync(
            request,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ab")]
    [InlineData(" ab ")]
    public async Task Owner_CreateWithInvalidName_ReturnsBadRequest(string name)
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await app.SeedUserAsync("owner", "owner-password");
        var workspace = await SeedWorkspaceAsync((owner, WorkspaceRole.Owner));
        var accessToken = await LoginAsync("owner", "owner-password");

        var response = await SendAsync(
            HttpMethod.Post,
            ProjectsPath(workspace.Id),
            accessToken,
            new { name });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var projectCount = await app.WithDatabaseAsync(db => db.Projects.CountAsync(ct));
        projectCount.ShouldBe(0);
    }

    [Fact]
    public async Task Owner_CreateTrimsNameAndAcceptsLengthBoundaries()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await app.SeedUserAsync("owner", "owner-password");
        var workspace = await app.SeedWorkspaceAsync((owner, WorkspaceRole.Owner));
        var accessToken = await app.LoginAsync(owner.Email, "owner-password");
        var maximumLengthName = new string('x', 100);

        var minimumResponse = await app.SendAsync(
            HttpMethod.Post,
            TestApp.ProjectsPath(workspace.Id),
            accessToken,
            new { name = " abc " });
        minimumResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var minimumProject = await minimumResponse.Content
            .ReadFromJsonAsync<CreateProjectResponse>(cancellationToken: ct);
        minimumProject.ShouldNotBeNull();
        minimumProject.Name.ShouldBe("abc");

        var maximumResponse = await app.SendAsync(
            HttpMethod.Post,
            TestApp.ProjectsPath(workspace.Id),
            accessToken,
            new { name = maximumLengthName });
        maximumResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var maximumProject = await maximumResponse.Content
            .ReadFromJsonAsync<CreateProjectResponse>(cancellationToken: ct);
        maximumProject.ShouldNotBeNull();
        maximumProject.Name.ShouldBe(maximumLengthName);

        var storedNames = await app.WithDatabaseAsync(
            db => db.Projects
                .OrderBy(project => project.Name)
                .Select(project => project.Name)
                .ToListAsync(ct));
        storedNames.ShouldBe(["abc", maximumLengthName]);
    }

    [Fact]
    public async Task Owner_CreateWithNameOverMaximum_ReturnsBadRequest()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await app.SeedUserAsync("owner", "owner-password");
        var workspace = await app.SeedWorkspaceAsync((owner, WorkspaceRole.Owner));
        var accessToken = await app.LoginAsync(owner.Email, "owner-password");

        var response = await app.SendAsync(
            HttpMethod.Post,
            TestApp.ProjectsPath(workspace.Id),
            accessToken,
            new { name = new string('x', 101) });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var projectCount = await app.WithDatabaseAsync(db => db.Projects.CountAsync(ct));
        projectCount.ShouldBe(0);
    }

    [Fact]
    public async Task Editor_InvalidUpdateDoesNotChangePersistedProject()
    {
        var ct = TestContext.Current.CancellationToken;
        var editor = await app.SeedUserAsync("editor", "editor-password");
        var workspace = await app.SeedWorkspaceAsync((editor, WorkspaceRole.Editor));
        var project = await app.SeedProjectAsync(workspace.Id, "Original project");
        var accessToken = await app.LoginAsync(editor.Email, "editor-password");

        foreach (var invalidName in new[] { " ab ", new string('x', 101) })
        {
            var response = await app.SendAsync(
                HttpMethod.Put,
                TestApp.ProjectPath(workspace.Id, project.Id),
                accessToken,
                new { name = invalidName });
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        var storedProject = await app.WithDatabaseAsync(
            db => db.Projects.AsNoTracking().SingleAsync(ct));
        storedProject.Name.ShouldBe("Original project");
        storedProject.UpdatedAt.ShouldBe(project.UpdatedAt);
    }

    [Fact]
    public async Task List_ReturnsOnlyRequestedWorkspacesProjectsInNameOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        var member = await app.SeedUserAsync("member", "member-password");
        var firstWorkspace = await app.SeedWorkspaceAsync((member, WorkspaceRole.Member));
        var secondWorkspace = await app.SeedWorkspaceAsync((member, WorkspaceRole.Member));
        await app.SeedProjectAsync(firstWorkspace.Id, "Zulu");
        await app.SeedProjectAsync(firstWorkspace.Id, "Alpha");
        await app.SeedProjectAsync(secondWorkspace.Id, "Other workspace");
        var accessToken = await app.LoginAsync(member.Email, "member-password");

        var response = await app.SendAsync(
            HttpMethod.Get,
            TestApp.ProjectsPath(firstWorkspace.Id),
            accessToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var projects = await response.Content.ReadFromJsonAsync<List<ListProjectsItemResponse>>(
            cancellationToken: ct);
        projects.ShouldNotBeNull();
        projects.Select(project => project.Name).ShouldBe(["Alpha", "Zulu"]);
        projects.ShouldAllBe(project => project.WorkspaceId == firstWorkspace.Id);
    }

    private async Task<Workspace> SeedWorkspaceAsync(
        params (User User, WorkspaceRole Role)[] members)
    {
        return await app.WithDatabaseAsync(
            async db =>
            {
                var workspace = new Workspace
                {
                    Name = $"Workspace {Guid.NewGuid():N}",
                    Memberships = members
                        .Select(member => new WorkspaceMembership
                        {
                            UserId = member.User.Id,
                            Role = member.Role
                        })
                        .ToList()
                };

                db.Workspaces.Add(workspace);
                await db.SaveChangesAsync();
                return workspace;
            });
    }

    private async Task<Project> SeedProjectAsync(Guid workspaceId, string name)
    {
        return await app.WithDatabaseAsync(
            async db =>
            {
                var now = DateTime.UtcNow;
                var project = new Project
                {
                    WorkspaceId = workspaceId,
                    Name = name,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                db.Projects.Add(project);
                await db.SaveChangesAsync();
                return project;
            });
    }

    private Task<string> LoginAsync(string email, string password) =>
        app.LoginAsync(email, password);

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        string accessToken,
        object? body = null)
    {
        return await app.SendAsync(method, path, accessToken, body);
    }

    private static string ProjectsPath(Guid workspaceId) =>
        $"/api/workspaces/{workspaceId}/projects";

    private static string ProjectPath(Guid workspaceId, Guid projectId) =>
        $"{ProjectsPath(workspaceId)}/{projectId}";
}
