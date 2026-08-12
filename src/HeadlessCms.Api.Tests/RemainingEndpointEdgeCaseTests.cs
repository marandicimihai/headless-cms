using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FastEndpoints.Testing;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Endpoints.Projects;
using HeadlessCms.Api.Endpoints.Workspaces;
using HeadlessCms.Api.Workspaces.Models;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace HeadlessCms.Api.Tests;

[Collection<TestAppCollection>]
public sealed class RemainingEndpointEdgeCaseTests(TestApp app) : TestBase
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    protected override async ValueTask SetupAsync() => await app.ResetDatabaseAsync();

    [Fact]
    public async Task CreateWorkspace_TrimsNameAndNormalizesOwnerEmail()
    {
        var admin = await app.SeedUserAsync(
            "admin",
            "password",
            PlatformRole.PlatformAdmin);
        var token = await app.LoginAsync(admin.Email, "password");

        using var response = await app.SendAsync(
            HttpMethod.Post,
            "/api/workspaces",
            token,
            new
            {
                name = "  Workspace A  ",
                ownerEmail = "  OWNER@EXAMPLE.TEST  "
            });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<CreateWorkspaceResponse>(
            JsonOptions,
            TestContext.Current.CancellationToken);
        created.ShouldNotBeNull();
        created.Workspace.Name.ShouldBe("Workspace A");
        created.OwnerInvitation.Email.ShouldBe("owner@example.test");
    }

    [Theory]
    [InlineData("", "owner@example.test")]
    [InlineData("   ", "owner@example.test")]
    [InlineData(
        "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
        "owner@example.test")]
    [InlineData("Workspace A", "not-an-email")]
    public async Task CreateWorkspace_RejectsInvalidInputWithoutPersisting(
        string name,
        string ownerEmail)
    {
        var admin = await app.SeedUserAsync(
            "admin",
            "password",
            PlatformRole.PlatformAdmin);
        var token = await app.LoginAsync(admin.Email, "password");

        using var response = await app.SendAsync(
            HttpMethod.Post,
            "/api/workspaces",
            token,
            new { name, ownerEmail });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var workspaceCount = await app.WithDatabaseAsync(db => db.Workspaces.CountAsync());
        workspaceCount.ShouldBe(0);
    }

    [Fact]
    public async Task ListMyWorkspaces_ReturnsAlphabeticalMembershipsAndRejectsAnonymous()
    {
        var user = await app.SeedUserAsync("member", "password");
        var zulu = await SeedNamedWorkspaceAsync("Zulu", user, WorkspaceRole.Member);
        var alpha = await SeedNamedWorkspaceAsync("Alpha", user, WorkspaceRole.Editor);
        var token = await app.LoginAsync(user.Email, "password");

        using var response = await app.SendAsync(
            HttpMethod.Get,
            "/api/me/workspaces",
            token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var workspaces = await response.Content.ReadFromJsonAsync<List<ListMyWorkspacesItemResponse>>(
            JsonOptions,
            TestContext.Current.CancellationToken);
        workspaces.ShouldNotBeNull();
        workspaces.Select(workspace => workspace.Id).ShouldBe([alpha.Id, zulu.Id]);
        workspaces.Select(workspace => workspace.CurrentRole)
            .ShouldBe([WorkspaceRole.Editor, WorkspaceRole.Member]);

        using var anonymous = await app.HttpsClient.GetAsync(
            "/api/me/workspaces",
            TestContext.Current.CancellationToken);
        anonymous.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateProject_TrimsNameAndAcceptsLengthBoundaries()
    {
        var owner = await app.SeedUserAsync("owner", "password");
        var workspace = await app.SeedWorkspaceAsync((owner, WorkspaceRole.Owner));
        var project = await app.SeedProjectAsync(workspace.Id, "Original");
        var token = await app.LoginAsync(owner.Email, "password");

        using var minimum = await app.SendAsync(
            HttpMethod.Put,
            TestApp.ProjectPath(workspace.Id, project.Id),
            token,
            new { name = " abc " });
        minimum.StatusCode.ShouldBe(HttpStatusCode.OK);
        var minimumResult = await minimum.Content.ReadFromJsonAsync<UpdateProjectResponse>(
            cancellationToken: TestContext.Current.CancellationToken);
        minimumResult.ShouldNotBeNull();
        minimumResult.Name.ShouldBe("abc");

        var maximumName = new string('x', 100);
        using var maximum = await app.SendAsync(
            HttpMethod.Put,
            TestApp.ProjectPath(workspace.Id, project.Id),
            token,
            new { name = maximumName });
        maximum.StatusCode.ShouldBe(HttpStatusCode.OK);
        var maximumResult = await maximum.Content.ReadFromJsonAsync<UpdateProjectResponse>(
            cancellationToken: TestContext.Current.CancellationToken);
        maximumResult.ShouldNotBeNull();
        maximumResult.Name.ShouldBe(maximumName);
    }

    [Fact]
    public async Task PlatformAdmin_ListingInvitationsForMissingWorkspaceReturnsNotFound()
    {
        var admin = await app.SeedUserAsync(
            "admin",
            "password",
            PlatformRole.PlatformAdmin);
        var token = await app.LoginAsync(admin.Email, "password");

        using var response = await app.SendAsync(
            HttpMethod.Get,
            $"/api/workspaces/{Guid.NewGuid()}/invitations",
            token);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private Task<Workspace> SeedNamedWorkspaceAsync(
        string name,
        User user,
        WorkspaceRole role) =>
        app.WithDatabaseAsync(
            async db =>
            {
                var workspace = new Workspace
                {
                    Name = name,
                    Memberships =
                    [
                        new WorkspaceMembership
                        {
                            UserId = user.Id,
                            Role = role
                        }
                    ]
                };
                db.Workspaces.Add(workspace);
                await db.SaveChangesAsync();
                return workspace;
            });

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
