using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FastEndpoints;
using FastEndpoints.Security;
using FastEndpoints.Testing;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Endpoints.Auth;
using HeadlessCms.Api.Endpoints.Projects;
using HeadlessCms.Api.Tenancy.Models;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace HeadlessCms.Api.Tests;

[Collection<TestAppCollection>]
public sealed class ProjectEndpointTests(TestApp app) : TestBase
{
    protected override async ValueTask SetupAsync()
    {
        await app.ResetDatabaseAsync();
    }

    [Fact]
    public async Task Owner_CanCreateReadUpdateAndDeleteTenantProject()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await app.SeedUserAsync("owner", "owner-password");
        var tenant = await SeedTenantAsync((owner, TenantRole.Owner));
        var accessToken = await LoginAsync("owner", "owner-password");

        var createResponse = await SendAsync(
            HttpMethod.Post,
            ProjectsPath(tenant.Id),
            accessToken,
            new { name = "Website" });

        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateProjectResponse>(
            cancellationToken: ct);
        created.ShouldNotBeNull();
        created.TenantId.ShouldBe(tenant.Id);
        created.Name.ShouldBe("Website");
        created.CreatedAt.ShouldBe(created.UpdatedAt);
        createResponse.Headers.Location.ShouldNotBeNull();
        createResponse.Headers.Location!.ToString().ShouldEndWith(
            $"{ProjectsPath(tenant.Id)}/{created.Id}");

        var storedTenantId = await app.WithDatabaseAsync(
            db => db.Projects
                .Where(project => project.Id == created.Id)
                .Select(project => project.TenantId)
                .SingleAsync(ct));
        storedTenantId.ShouldBe(tenant.Id);

        var listResponse = await SendAsync(
            HttpMethod.Get,
            ProjectsPath(tenant.Id),
            accessToken);
        listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var projects = await listResponse.Content.ReadFromJsonAsync<List<ListProjectsItemResponse>>(
            cancellationToken: ct);
        projects.ShouldNotBeNull();
        projects.Select(project => project.Id).ShouldBe([created.Id]);

        var getResponse = await SendAsync(
            HttpMethod.Get,
            ProjectPath(tenant.Id, created.Id),
            accessToken);
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var updateResponse = await SendAsync(
            HttpMethod.Put,
            ProjectPath(tenant.Id, created.Id),
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
            ProjectPath(tenant.Id, created.Id),
            accessToken);
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var projectCount = await app.WithDatabaseAsync(db => db.Projects.CountAsync(ct));
        projectCount.ShouldBe(0);
    }

    [Fact]
    public async Task Editor_CanCreateUpdateAndDeleteTenantProject()
    {
        var ct = TestContext.Current.CancellationToken;
        var editor = await app.SeedUserAsync("editor", "editor-password");
        var tenant = await SeedTenantAsync((editor, TenantRole.Editor));
        var accessToken = await LoginAsync("editor", "editor-password");

        var createResponse = await SendAsync(
            HttpMethod.Post,
            ProjectsPath(tenant.Id),
            accessToken,
            new { name = "Editor project" });
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var project = await createResponse.Content.ReadFromJsonAsync<CreateProjectResponse>(
            cancellationToken: ct);
        project.ShouldNotBeNull();

        var updateResponse = await SendAsync(
            HttpMethod.Put,
            ProjectPath(tenant.Id, project.Id),
            accessToken,
            new { name = "Edited project" });
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var deleteResponse = await SendAsync(
            HttpMethod.Delete,
            ProjectPath(tenant.Id, project.Id),
            accessToken);
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Member_CanReadTenantProjectsButCannotWrite()
    {
        var ct = TestContext.Current.CancellationToken;
        var member = await app.SeedUserAsync("member", "member-password");
        var tenant = await SeedTenantAsync((member, TenantRole.Member));
        var project = await SeedProjectAsync(tenant.Id, "Read-only project");
        var accessToken = await LoginAsync("member", "member-password");

        var listResponse = await SendAsync(
            HttpMethod.Get,
            ProjectsPath(tenant.Id),
            accessToken);
        listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var projects = await listResponse.Content.ReadFromJsonAsync<List<ListProjectsItemResponse>>(
            cancellationToken: ct);
        projects.ShouldNotBeNull();
        projects.Select(candidate => candidate.Id).ShouldBe([project.Id]);

        var getResponse = await SendAsync(
            HttpMethod.Get,
            ProjectPath(tenant.Id, project.Id),
            accessToken);
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var createResponse = await SendAsync(
            HttpMethod.Post,
            ProjectsPath(tenant.Id),
            accessToken,
            new { name = "Forbidden project" });
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var updateResponse = await SendAsync(
            HttpMethod.Put,
            ProjectPath(tenant.Id, project.Id),
            accessToken,
            new { name = "Forbidden update" });
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var deleteResponse = await SendAsync(
            HttpMethod.Delete,
            ProjectPath(tenant.Id, project.Id),
            accessToken);
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var storedProject = await app.WithDatabaseAsync(
            db => db.Projects.AsNoTracking().SingleAsync(ct));
        storedProject.Name.ShouldBe("Read-only project");
    }

    [Fact]
    public async Task NonMember_CannotDiscoverOrMutateTenantProjects()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await app.SeedUserAsync("owner", "owner-password");
        var outsider = await app.SeedUserAsync("outsider", "outsider-password");
        var tenant = await SeedTenantAsync((owner, TenantRole.Owner));
        var project = await SeedProjectAsync(tenant.Id, "Private project");
        var accessToken = await LoginAsync(outsider.Email, "outsider-password");

        var listResponse = await SendAsync(
            HttpMethod.Get,
            ProjectsPath(tenant.Id),
            accessToken);
        listResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var getResponse = await SendAsync(
            HttpMethod.Get,
            ProjectPath(tenant.Id, project.Id),
            accessToken);
        getResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var createResponse = await SendAsync(
            HttpMethod.Post,
            ProjectsPath(tenant.Id),
            accessToken,
            new { name = "Stolen project" });
        createResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var updateResponse = await SendAsync(
            HttpMethod.Put,
            ProjectPath(tenant.Id, project.Id),
            accessToken,
            new { name = "Stolen project" });
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var deleteResponse = await SendAsync(
            HttpMethod.Delete,
            ProjectPath(tenant.Id, project.Id),
            accessToken);
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var storedProject = await app.WithDatabaseAsync(
            db => db.Projects.AsNoTracking().SingleAsync(ct));
        storedProject.Name.ShouldBe("Private project");
        storedProject.TenantId.ShouldBe(tenant.Id);
    }

    [Fact]
    public async Task ProjectId_CannotBeUsedThroughAnotherTenantRoute()
    {
        var editor = await app.SeedUserAsync("editor", "editor-password");
        var firstTenant = await SeedTenantAsync((editor, TenantRole.Editor));
        var secondTenant = await SeedTenantAsync((editor, TenantRole.Editor));
        var secondTenantProject = await SeedProjectAsync(secondTenant.Id, "Other tenant project");
        var accessToken = await LoginAsync("editor", "editor-password");

        var getResponse = await SendAsync(
            HttpMethod.Get,
            ProjectPath(firstTenant.Id, secondTenantProject.Id),
            accessToken);
        getResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var updateResponse = await SendAsync(
            HttpMethod.Put,
            ProjectPath(firstTenant.Id, secondTenantProject.Id),
            accessToken,
            new { name = "Cross-tenant update" });
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var deleteResponse = await SendAsync(
            HttpMethod.Delete,
            ProjectPath(firstTenant.Id, secondTenantProject.Id),
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
        var tenantId = Guid.NewGuid();
        var path = includeProjectId
            ? ProjectPath(tenantId, Guid.NewGuid())
            : ProjectsPath(tenantId);
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
        var tenant = await SeedTenantAsync((owner, TenantRole.Owner));
        var accessToken = await LoginAsync("owner", "owner-password");

        var response = await SendAsync(
            HttpMethod.Post,
            ProjectsPath(tenant.Id),
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
        var tenant = await app.SeedTenantAsync((owner, TenantRole.Owner));
        var accessToken = await app.LoginAsync(owner.Email, "owner-password");
        var maximumLengthName = new string('x', 100);

        var minimumResponse = await app.SendAsync(
            HttpMethod.Post,
            TestApp.ProjectsPath(tenant.Id),
            accessToken,
            new { name = " abc " });
        minimumResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var minimumProject = await minimumResponse.Content
            .ReadFromJsonAsync<CreateProjectResponse>(cancellationToken: ct);
        minimumProject.ShouldNotBeNull();
        minimumProject.Name.ShouldBe("abc");

        var maximumResponse = await app.SendAsync(
            HttpMethod.Post,
            TestApp.ProjectsPath(tenant.Id),
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
        var tenant = await app.SeedTenantAsync((owner, TenantRole.Owner));
        var accessToken = await app.LoginAsync(owner.Email, "owner-password");

        var response = await app.SendAsync(
            HttpMethod.Post,
            TestApp.ProjectsPath(tenant.Id),
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
        var tenant = await app.SeedTenantAsync((editor, TenantRole.Editor));
        var project = await app.SeedProjectAsync(tenant.Id, "Original project");
        var accessToken = await app.LoginAsync(editor.Email, "editor-password");

        foreach (var invalidName in new[] { " ab ", new string('x', 101) })
        {
            var response = await app.SendAsync(
                HttpMethod.Put,
                TestApp.ProjectPath(tenant.Id, project.Id),
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
    public async Task List_ReturnsOnlyRequestedTenantsProjectsInNameOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        var member = await app.SeedUserAsync("member", "member-password");
        var firstTenant = await app.SeedTenantAsync((member, TenantRole.Member));
        var secondTenant = await app.SeedTenantAsync((member, TenantRole.Member));
        await app.SeedProjectAsync(firstTenant.Id, "Zulu");
        await app.SeedProjectAsync(firstTenant.Id, "Alpha");
        await app.SeedProjectAsync(secondTenant.Id, "Other tenant");
        var accessToken = await app.LoginAsync(member.Email, "member-password");

        var response = await app.SendAsync(
            HttpMethod.Get,
            TestApp.ProjectsPath(firstTenant.Id),
            accessToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var projects = await response.Content.ReadFromJsonAsync<List<ListProjectsItemResponse>>(
            cancellationToken: ct);
        projects.ShouldNotBeNull();
        projects.Select(project => project.Name).ShouldBe(["Alpha", "Zulu"]);
        projects.ShouldAllBe(project => project.TenantId == firstTenant.Id);
    }

    private async Task<Tenant> SeedTenantAsync(
        params (User User, TenantRole Role)[] members)
    {
        return await app.WithDatabaseAsync(
            async db =>
            {
                var tenant = new Tenant
                {
                    Name = $"Tenant {Guid.NewGuid():N}",
                    Memberships = members
                        .Select(member => new TenantMembership
                        {
                            UserId = member.User.Id,
                            Role = member.Role
                        })
                        .ToList()
                };

                db.Tenants.Add(tenant);
                await db.SaveChangesAsync();
                return tenant;
            });
    }

    private async Task<Project> SeedProjectAsync(Guid tenantId, string name)
    {
        return await app.WithDatabaseAsync(
            async db =>
            {
                var now = DateTime.UtcNow;
                var project = new Project
                {
                    TenantId = tenantId,
                    Name = name,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                db.Projects.Add(project);
                await db.SaveChangesAsync();
                return project;
            });
    }

    private async Task<string> LoginAsync(string email, string password)
    {
        var (response, tokens) =
            await app.HttpsClient.POSTAsync<Login, LoginRequest, TokenResponse>(
                new LoginRequest
                {
                    Email = TestApp.AsEmail(email),
                    Password = password
                });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return tokens.AccessToken;
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        string accessToken,
        object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (body is not null)
            request.Content = JsonContent.Create(body);

        return await app.HttpsClient.SendAsync(request);
    }

    private static string ProjectsPath(Guid tenantId) =>
        $"/api/tenants/{tenantId}/projects";

    private static string ProjectPath(Guid tenantId, Guid projectId) =>
        $"{ProjectsPath(tenantId)}/{projectId}";
}
