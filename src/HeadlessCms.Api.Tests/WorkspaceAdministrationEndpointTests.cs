using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FastEndpoints.Testing;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Endpoints.Workspaces;
using HeadlessCms.Api.Workspaces.Models;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace HeadlessCms.Api.Tests;

[Collection<TestAppCollection>]
public sealed class WorkspaceAdministrationEndpointTests(TestApp app) : TestBase
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    protected override async ValueTask SetupAsync() => await app.ResetDatabaseAsync();

    [Fact]
    public async Task ListWorkspaces_PlatformAdminGetsRequestedPageInNameOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        var admin = await app.SeedUserAsync(
            "admin",
            "admin-password",
            PlatformRole.PlatformAdmin);
        await SeedWorkspaceAsync("Zulu");
        await SeedWorkspaceAsync("Bravo");
        await SeedWorkspaceAsync("Echo");
        await SeedWorkspaceAsync("Alpha");
        var accessToken = await app.LoginAsync(admin.Email, "admin-password");

        using var response = await app.SendAsync(
            HttpMethod.Get,
            "/api/workspaces?page=2&pageSize=2",
            accessToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<ListWorkspacesResponse>(
            JsonOptions,
            cancellationToken: ct);
        page.ShouldNotBeNull();
        page.Page.ShouldBe(2);
        page.PageSize.ShouldBe(2);
        page.Total.ShouldBe(4);
        page.Items.Select(workspace => workspace.Name).ShouldBe(["Echo", "Zulu"]);
        page.Items.ShouldAllBe(workspace => workspace.CurrentRole == null);
    }

    [Fact]
    public async Task ListWorkspaces_NormalizesPaginationBoundsAndReturnsEmptyOutOfRangePage()
    {
        var ct = TestContext.Current.CancellationToken;
        var admin = await app.SeedUserAsync(
            "admin",
            "admin-password",
            PlatformRole.PlatformAdmin);
        await SeedWorkspaceAsync("Bravo");
        await SeedWorkspaceAsync("Alpha");
        var accessToken = await app.LoginAsync(admin.Email, "admin-password");

        using var minimumResponse = await app.SendAsync(
            HttpMethod.Get,
            "/api/workspaces?page=0&pageSize=0",
            accessToken);
        minimumResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var minimumPage = await minimumResponse.Content
            .ReadFromJsonAsync<ListWorkspacesResponse>(
                JsonOptions,
                cancellationToken: ct);
        minimumPage.ShouldNotBeNull();
        minimumPage.Page.ShouldBe(1);
        minimumPage.PageSize.ShouldBe(1);
        minimumPage.Total.ShouldBe(2);
        minimumPage.Items.Select(workspace => workspace.Name).ShouldBe(["Alpha"]);

        using var maximumResponse = await app.SendAsync(
            HttpMethod.Get,
            "/api/workspaces?page=1&pageSize=101",
            accessToken);
        maximumResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var maximumPage = await maximumResponse.Content
            .ReadFromJsonAsync<ListWorkspacesResponse>(
                JsonOptions,
                cancellationToken: ct);
        maximumPage.ShouldNotBeNull();
        maximumPage.Page.ShouldBe(1);
        maximumPage.PageSize.ShouldBe(100);
        maximumPage.Total.ShouldBe(2);
        maximumPage.Items.Select(workspace => workspace.Name).ShouldBe(["Alpha", "Bravo"]);

        using var outOfRangeResponse = await app.SendAsync(
            HttpMethod.Get,
            "/api/workspaces?page=3&pageSize=1",
            accessToken);
        outOfRangeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var outOfRangePage = await outOfRangeResponse.Content
            .ReadFromJsonAsync<ListWorkspacesResponse>(
                JsonOptions,
                cancellationToken: ct);
        outOfRangePage.ShouldNotBeNull();
        outOfRangePage.Page.ShouldBe(3);
        outOfRangePage.PageSize.ShouldBe(1);
        outOfRangePage.Total.ShouldBe(2);
        outOfRangePage.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListWorkspaces_RejectsWorkspaceMemberAndOutsider()
    {
        var member = await app.SeedUserAsync("member", "member-password");
        var outsider = await app.SeedUserAsync("outsider", "outsider-password");
        await SeedWorkspaceAsync("Private workspace", (member, WorkspaceRole.Member));
        var memberToken = await app.LoginAsync(member.Email, "member-password");
        var outsiderToken = await app.LoginAsync(outsider.Email, "outsider-password");

        using var memberResponse = await app.SendAsync(
            HttpMethod.Get,
            "/api/workspaces",
            memberToken);
        memberResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var outsiderResponse = await app.SendAsync(
            HttpMethod.Get,
            "/api/workspaces",
            outsiderToken);
        outsiderResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetWorkspace_ReturnsWorkspaceToPlatformAdminAndMembersWithTheirCurrentRole()
    {
        var ct = TestContext.Current.CancellationToken;
        var admin = await app.SeedUserAsync(
            "admin",
            "admin-password",
            PlatformRole.PlatformAdmin);
        var owner = await app.SeedUserAsync("owner", "owner-password");
        var editor = await app.SeedUserAsync("editor", "editor-password");
        var member = await app.SeedUserAsync("member", "member-password");
        var workspace = await SeedWorkspaceAsync(
            "Visible workspace",
            (owner, WorkspaceRole.Owner),
            (editor, WorkspaceRole.Editor),
            (member, WorkspaceRole.Member));

        var callers = new[]
        {
            (User: admin, Password: "admin-password", ExpectedRole: (WorkspaceRole?)null),
            (User: owner, Password: "owner-password", ExpectedRole: (WorkspaceRole?)WorkspaceRole.Owner),
            (User: editor, Password: "editor-password", ExpectedRole: (WorkspaceRole?)WorkspaceRole.Editor),
            (User: member, Password: "member-password", ExpectedRole: (WorkspaceRole?)WorkspaceRole.Member)
        };

        foreach (var caller in callers)
        {
            var accessToken = await app.LoginAsync(caller.User.Email, caller.Password);
            using var response = await app.SendAsync(
                HttpMethod.Get,
                WorkspacePath(workspace.Id),
                accessToken);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var returnedWorkspace = await response.Content.ReadFromJsonAsync<GetWorkspaceResponse>(
                JsonOptions,
                cancellationToken: ct);
            returnedWorkspace.ShouldNotBeNull();
            returnedWorkspace.Id.ShouldBe(workspace.Id);
            returnedWorkspace.Name.ShouldBe("Visible workspace");
            returnedWorkspace.CreatedAt.ShouldBe(
                workspace.CreatedAt,
                TimeSpan.FromMilliseconds(1));
            returnedWorkspace.CurrentRole.ShouldBe(caller.ExpectedRole);
        }
    }

    [Fact]
    public async Task GetWorkspace_HidesExistingWorkspaceFromOutsiderAndReturnsNotFoundForUnknownWorkspace()
    {
        var admin = await app.SeedUserAsync(
            "admin",
            "admin-password",
            PlatformRole.PlatformAdmin);
        var member = await app.SeedUserAsync("member", "member-password");
        var outsider = await app.SeedUserAsync("outsider", "outsider-password");
        var workspace = await SeedWorkspaceAsync(
            "Private workspace",
            (member, WorkspaceRole.Member));
        var adminToken = await app.LoginAsync(admin.Email, "admin-password");
        var memberToken = await app.LoginAsync(member.Email, "member-password");
        var outsiderToken = await app.LoginAsync(outsider.Email, "outsider-password");
        var missingWorkspaceId = Guid.NewGuid();

        using var outsiderResponse = await app.SendAsync(
            HttpMethod.Get,
            WorkspacePath(workspace.Id),
            outsiderToken);
        outsiderResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        using var adminResponse = await app.SendAsync(
            HttpMethod.Get,
            WorkspacePath(missingWorkspaceId),
            adminToken);
        adminResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        using var memberResponse = await app.SendAsync(
            HttpMethod.Get,
            WorkspacePath(missingWorkspaceId),
            memberToken);
        memberResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RenameWorkspace_PlatformAdminAndOwnerCanRenameAndNamesAreTrimmed()
    {
        var ct = TestContext.Current.CancellationToken;
        var admin = await app.SeedUserAsync(
            "admin",
            "admin-password",
            PlatformRole.PlatformAdmin);
        var owner = await app.SeedUserAsync("owner", "owner-password");
        var workspace = await SeedWorkspaceAsync(
            "Original workspace",
            (owner, WorkspaceRole.Owner));
        var adminToken = await app.LoginAsync(admin.Email, "admin-password");
        var ownerToken = await app.LoginAsync(owner.Email, "owner-password");

        using var adminResponse = await app.SendAsync(
            HttpMethod.Patch,
            WorkspacePath(workspace.Id),
            adminToken,
            new { name = "  Admin renamed  " });
        adminResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var adminResult = await adminResponse.Content.ReadFromJsonAsync<RenameWorkspaceResponse>(
            JsonOptions,
            cancellationToken: ct);
        adminResult.ShouldNotBeNull();
        adminResult.Name.ShouldBe("Admin renamed");
        adminResult.CurrentRole.ShouldBeNull();

        using var ownerResponse = await app.SendAsync(
            HttpMethod.Patch,
            WorkspacePath(workspace.Id),
            ownerToken,
            new { name = "  Owner renamed  " });
        ownerResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var ownerResult = await ownerResponse.Content.ReadFromJsonAsync<RenameWorkspaceResponse>(
            JsonOptions,
            cancellationToken: ct);
        ownerResult.ShouldNotBeNull();
        ownerResult.Name.ShouldBe("Owner renamed");
        ownerResult.CurrentRole.ShouldBe(WorkspaceRole.Owner);

        var storedName = await app.WithDatabaseAsync(
            db => db.Workspaces
                .Where(candidate => candidate.Id == workspace.Id)
                .Select(candidate => candidate.Name)
                .SingleAsync(ct));
        storedName.ShouldBe("Owner renamed");
    }

    [Fact]
    public async Task RenameWorkspace_RejectsEditorMemberAndOutsiderWithoutChangingWorkspace()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await app.SeedUserAsync("owner", "owner-password");
        var editor = await app.SeedUserAsync("editor", "editor-password");
        var member = await app.SeedUserAsync("member", "member-password");
        var outsider = await app.SeedUserAsync("outsider", "outsider-password");
        var workspace = await SeedWorkspaceAsync(
            "Original workspace",
            (owner, WorkspaceRole.Owner),
            (editor, WorkspaceRole.Editor),
            (member, WorkspaceRole.Member));

        var unauthorizedCallers = new[]
        {
            (User: editor, Password: "editor-password"),
            (User: member, Password: "member-password"),
            (User: outsider, Password: "outsider-password")
        };

        foreach (var caller in unauthorizedCallers)
        {
            var accessToken = await app.LoginAsync(caller.User.Email, caller.Password);
            using var response = await app.SendAsync(
                HttpMethod.Patch,
                WorkspacePath(workspace.Id),
                accessToken,
                new { name = $"Renamed by {caller.User.Email}" });
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        var storedName = await app.WithDatabaseAsync(
            db => db.Workspaces
                .Where(candidate => candidate.Id == workspace.Id)
                .Select(candidate => candidate.Name)
                .SingleAsync(ct));
        storedName.ShouldBe("Original workspace");
    }

    [Fact]
    public async Task RenameWorkspace_ReturnsNotFoundForUnknownWorkspace()
    {
        var admin = await app.SeedUserAsync(
            "admin",
            "admin-password",
            PlatformRole.PlatformAdmin);
        var owner = await app.SeedUserAsync("owner", "owner-password");
        await SeedWorkspaceAsync("Owned workspace", (owner, WorkspaceRole.Owner));
        var adminToken = await app.LoginAsync(admin.Email, "admin-password");
        var ownerToken = await app.LoginAsync(owner.Email, "owner-password");
        var missingWorkspacePath = WorkspacePath(Guid.NewGuid());

        using var adminResponse = await app.SendAsync(
            HttpMethod.Patch,
            missingWorkspacePath,
            adminToken,
            new { name = "Missing workspace" });
        adminResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        using var ownerResponse = await app.SendAsync(
            HttpMethod.Patch,
            missingWorkspacePath,
            ownerToken,
            new { name = "Missing workspace" });
        ownerResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RenameWorkspace_ValidatesNameBoundariesAndPreservesPersistedValue()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await app.SeedUserAsync("owner", "owner-password");
        var workspace = await SeedWorkspaceAsync(
            "Original workspace",
            (owner, WorkspaceRole.Owner));
        var ownerToken = await app.LoginAsync(owner.Email, "owner-password");

        foreach (var invalidName in new[] { "", "   ", new string('x', 101) })
        {
            using var response = await app.SendAsync(
                HttpMethod.Patch,
                WorkspacePath(workspace.Id),
                ownerToken,
                new { name = invalidName });
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        var nameAfterInvalidRequests = await app.WithDatabaseAsync(
            db => db.Workspaces
                .Where(candidate => candidate.Id == workspace.Id)
                .Select(candidate => candidate.Name)
                .SingleAsync(ct));
        nameAfterInvalidRequests.ShouldBe("Original workspace");

        var maximumLengthName = new string('x', 100);
        using var boundaryResponse = await app.SendAsync(
            HttpMethod.Patch,
            WorkspacePath(workspace.Id),
            ownerToken,
            new { name = maximumLengthName });
        boundaryResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var boundaryResult = await boundaryResponse.Content.ReadFromJsonAsync<RenameWorkspaceResponse>(
            JsonOptions,
            cancellationToken: ct);
        boundaryResult.ShouldNotBeNull();
        boundaryResult.Name.ShouldBe(maximumLengthName);

        var storedBoundaryName = await app.WithDatabaseAsync(
            db => db.Workspaces
                .Where(candidate => candidate.Id == workspace.Id)
                .Select(candidate => candidate.Name)
                .SingleAsync(ct));
        storedBoundaryName.ShouldBe(maximumLengthName);
    }

    [Theory]
    [InlineData("GET", "/api/workspaces", false)]
    [InlineData("GET", "/api/workspaces/00000000-0000-0000-0000-000000000001", false)]
    [InlineData("PATCH", "/api/workspaces/00000000-0000-0000-0000-000000000001", true)]
    public async Task WorkspaceAdministrationEndpoints_RejectAnonymousRequests(
        string method,
        string path,
        bool includeBody)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (includeBody)
            request.Content = JsonContent.Create(new { name = "Private workspace" });

        using var response = await app.HttpsClient.SendAsync(
            request,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private async Task<Workspace> SeedWorkspaceAsync(
        string name,
        params (User User, WorkspaceRole Role)[] members)
    {
        return await app.WithDatabaseAsync(
            async db =>
            {
                var workspace = new Workspace
                {
                    Name = name,
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

    private static string WorkspacePath(Guid workspaceId) => $"/api/workspaces/{workspaceId}";

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
