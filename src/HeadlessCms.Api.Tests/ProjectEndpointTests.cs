using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FastEndpoints;
using FastEndpoints.Security;
using FastEndpoints.Testing;
using HeadlessCms.Api.Endpoints.Auth;
using HeadlessCms.Api.Endpoints.Projects;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace HeadlessCms.Api.Tests;

public sealed class ProjectEndpointTests(AuthApp app) : TestBase<AuthApp>
{
    protected override async ValueTask SetupAsync()
    {
        await app.ResetDatabaseAsync();
    }

    [Fact]
    public async Task Owner_CanCreateReadUpdateAndDeleteProject()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = await app.SeedUserAsync("owner", "owner-password");
        var accessToken = await LoginAsync("owner", "owner-password");

        var createResponse = await SendAsync(
            HttpMethod.Post,
            "/api/projects",
            accessToken,
            new { name = "Website" });

        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<ProjectResponse>(
            cancellationToken: ct);
        created.ShouldNotBeNull();
        created.Name.ShouldBe("Website");
        created.CreatedAt.ShouldBe(created.UpdatedAt);
        createResponse.Headers.Location.ShouldNotBeNull();
        createResponse.Headers.Location!.ToString().ShouldEndWith($"/api/projects/{created.Id}");

        var storedOwnerId = await app.WithDatabaseAsync(
            db => db.Projects
                .Where(project => project.Id == created.Id)
                .Select(project => project.OwnerId)
                .SingleAsync());
        storedOwnerId.ShouldBe(user.Id);

        var listResponse = await SendAsync(HttpMethod.Get, "/api/projects", accessToken);
        listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var projects = await listResponse.Content
            .ReadFromJsonAsync<List<ProjectResponse>>(cancellationToken: ct);
        projects.ShouldNotBeNull();
        projects.Count.ShouldBe(1);
        projects[0].Id.ShouldBe(created.Id);

        var getResponse = await SendAsync(
            HttpMethod.Get,
            $"/api/projects/{created.Id}",
            accessToken);
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var fetched = await getResponse.Content.ReadFromJsonAsync<ProjectResponse>(
            cancellationToken: ct);
        fetched.ShouldNotBeNull();
        fetched.Id.ShouldBe(created.Id);

        var updateResponse = await SendAsync(
            HttpMethod.Put,
            $"/api/projects/{created.Id}",
            accessToken,
            new { name = "Updated website" });
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<ProjectResponse>(
            cancellationToken: ct);
        updated.ShouldNotBeNull();
        updated.Name.ShouldBe("Updated website");
        updated.UpdatedAt.ShouldBeGreaterThanOrEqualTo(created.UpdatedAt);

        var deleteResponse = await SendAsync(
            HttpMethod.Delete,
            $"/api/projects/{created.Id}",
            accessToken);
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var projectCount = await app.WithDatabaseAsync(db => db.Projects.CountAsync());
        projectCount.ShouldBe(0);
    }

    [Fact]
    public async Task User_CannotDiscoverOrMutateAnotherUsersProject()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await app.SeedUserAsync("owner", "owner-password");
        var ownerToken = await LoginAsync("owner", "owner-password");
        await app.SeedUserAsync("intruder", "intruder-password");
        var intruderToken = await LoginAsync("intruder", "intruder-password");

        var createResponse = await SendAsync(
            HttpMethod.Post,
            "/api/projects",
            ownerToken,
            new { name = "Private project" });
        var project = await createResponse.Content.ReadFromJsonAsync<ProjectResponse>(
            cancellationToken: ct);
        project.ShouldNotBeNull();

        var listResponse = await SendAsync(HttpMethod.Get, "/api/projects", intruderToken);
        var visibleProjects = await listResponse.Content
            .ReadFromJsonAsync<List<ProjectResponse>>(cancellationToken: ct);
        visibleProjects.ShouldNotBeNull();
        visibleProjects.ShouldBeEmpty();

        var getResponse = await SendAsync(
            HttpMethod.Get,
            $"/api/projects/{project.Id}",
            intruderToken);
        getResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var updateResponse = await SendAsync(
            HttpMethod.Put,
            $"/api/projects/{project.Id}",
            intruderToken,
            new { name = "Stolen project" });
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var deleteResponse = await SendAsync(
            HttpMethod.Delete,
            $"/api/projects/{project.Id}",
            intruderToken);
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var storedProject = await app.WithDatabaseAsync(
            db => db.Projects.AsNoTracking().SingleAsync());
        storedProject.OwnerId.ShouldBe(owner.Id);
        storedProject.Name.ShouldBe("Private project");
    }

    [Theory]
    [InlineData("GET", "/api/projects", false)]
    [InlineData("GET", "/api/projects/11111111-1111-1111-1111-111111111111", false)]
    [InlineData("POST", "/api/projects", true)]
    [InlineData("PUT", "/api/projects/11111111-1111-1111-1111-111111111111", true)]
    [InlineData("DELETE", "/api/projects/11111111-1111-1111-1111-111111111111", false)]
    public async Task ProjectEndpoints_RejectAnonymousRequests(
        string method,
        string path,
        bool includeBody)
    {
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
    public async Task Create_WithInvalidName_ReturnsBadRequest(string name)
    {
        await app.SeedUserAsync("owner", "owner-password");
        var accessToken = await LoginAsync("owner", "owner-password");

        var response = await SendAsync(
            HttpMethod.Post,
            "/api/projects",
            accessToken,
            new { name });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var projectCount = await app.WithDatabaseAsync(db => db.Projects.CountAsync());
        projectCount.ShouldBe(0);
    }

    private async Task<string> LoginAsync(string username, string password)
    {
        var (response, tokens) =
            await app.HttpsClient.POSTAsync<Login, LoginRequest, TokenResponse>(
                new LoginRequest
                {
                    Username = username,
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
}
