using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FastEndpoints.Testing;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Endpoints;
using HeadlessCms.Api.Endpoints.Workspaces;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace HeadlessCms.Api.Tests;

[Collection<WorkspaceMembershipCollection>]
public sealed class WorkspaceMembershipEndpointTests(WorkspaceMembershipTestApp app) : TestBase
{
    private string databaseName = null!;
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    protected override async ValueTask SetupAsync()
    {
        databaseName = app.BeginTestDatabase();
        await app.InitializeDatabaseAsync();
    }

    protected override async ValueTask TearDownAsync() =>
        await app.CleanupDatabaseAsync(databaseName);

    [Fact]
    public async Task Owner_CanRemoveMember()
    {
        var setup = await CreateMembershipSetupAsync();
        var ct = TestContext.Current.CancellationToken;

        using var response = await app.SendAsync(
            HttpMethod.Delete,
            MemberPath(setup.WorkspaceId, setup.Member.Id),
            setup.OwnerToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var membershipExists = await app.WithDatabaseAsync(
            db => db.WorkspaceMemberships.AnyAsync(
                membership =>
                    membership.WorkspaceId == setup.WorkspaceId &&
                    membership.UserId == setup.Member.Id));
        membershipExists.ShouldBeFalse();

        using var removedMemberWorkspaces = await app.SendAsync(
            HttpMethod.Get,
            "/api/me/workspaces",
            setup.MemberToken);
        var workspaces = await removedMemberWorkspaces.Content
            .ReadFromJsonAsync<IReadOnlyList<ListMyWorkspacesItemResponse>>(JsonOptions, ct);
        workspaces.ShouldNotBeNull();
        workspaces.ShouldBeEmpty();
    }

    [Fact]
    public async Task NonOwner_CannotRemoveMember()
    {
        var setup = await CreateMembershipSetupAsync();

        using var response = await app.SendAsync(
            HttpMethod.Delete,
            MemberPath(setup.WorkspaceId, setup.Member.Id),
            setup.EditorToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await MembershipExistsAsync(setup.WorkspaceId, setup.Member.Id)).ShouldBeTrue();
    }

    [Fact]
    public async Task Owner_CannotRemoveOwnerOrUnknownMember()
    {
        var setup = await CreateMembershipSetupAsync();

        using var removeOwner = await app.SendAsync(
            HttpMethod.Delete,
            MemberPath(setup.WorkspaceId, setup.Owner.Id),
            setup.OwnerToken);
        removeOwner.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        using var removeUnknown = await app.SendAsync(
            HttpMethod.Delete,
            MemberPath(setup.WorkspaceId, setup.Outsider.Id),
            setup.OwnerToken);
        removeUnknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var memberships = await app.WithDatabaseAsync(
            db => db.WorkspaceMemberships
                .AsNoTracking()
                .Where(membership => membership.WorkspaceId == setup.WorkspaceId)
                .ToDictionaryAsync(membership => membership.UserId, membership => membership.Role));
        memberships.Count.ShouldBe(3);
        memberships[setup.Owner.Id].ShouldBe(WorkspaceRole.Owner);
    }

    [Fact]
    public async Task ChangeRole_RejectsUnsupportedRoles()
    {
        var setup = await CreateMembershipSetupAsync();
        var ct = TestContext.Current.CancellationToken;
        object[] unsupportedRoles = ["Owner"];

        foreach (var role in unsupportedRoles)
        {
            using var response = await app.SendAsync(
                HttpMethod.Patch,
                MemberPath(setup.WorkspaceId, setup.Member.Id),
                setup.OwnerToken,
                new { role });

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            var problem = await response.Content.ReadFromJsonAsync<ApiProblem>(JsonOptions, ct);
            problem.ShouldNotBeNull();
            problem.Code.ShouldBe("invalid_role");
        }

        (await GetMembershipRoleAsync(setup.WorkspaceId, setup.Member.Id))
            .ShouldBe(WorkspaceRole.Member);
    }

    [Fact]
    public async Task NonOwner_CannotChangeMemberRole()
    {
        var setup = await CreateMembershipSetupAsync();

        using var response = await app.SendAsync(
            HttpMethod.Patch,
            MemberPath(setup.WorkspaceId, setup.Member.Id),
            setup.EditorToken,
            new { role = "Editor" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await GetMembershipRoleAsync(setup.WorkspaceId, setup.Member.Id))
            .ShouldBe(WorkspaceRole.Member);
    }

    [Fact]
    public async Task ChangeRole_ReturnsNotFoundForMissingMember()
    {
        var setup = await CreateMembershipSetupAsync();

        using var response = await app.SendAsync(
            HttpMethod.Patch,
            MemberPath(setup.WorkspaceId, setup.Outsider.Id),
            setup.OwnerToken,
            new { role = "Editor" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Owner_CannotChangeOwnerRoleThroughMemberEndpoint()
    {
        var setup = await CreateMembershipSetupAsync();

        using var response = await app.SendAsync(
            HttpMethod.Patch,
            MemberPath(setup.WorkspaceId, setup.Owner.Id),
            setup.OwnerToken,
            new { role = "Member" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await GetMembershipRoleAsync(setup.WorkspaceId, setup.Owner.Id))
            .ShouldBe(WorkspaceRole.Owner);
    }

    [Fact]
    public async Task OwnershipTransfer_ReturnsNotFoundForInvalidTargets()
    {
        var setup = await CreateMembershipSetupAsync();
        var invalidTargets = new[]
        {
            setup.Owner.Id,
            setup.Outsider.Id,
            Guid.NewGuid().ToString()
        };

        foreach (var targetId in invalidTargets)
        {
            using var response = await app.SendAsync(
                HttpMethod.Post,
                $"/api/workspaces/{setup.WorkspaceId}/ownership-transfer",
                setup.OwnerToken,
                new { newOwnerUserId = targetId });
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        (await GetMembershipRoleAsync(setup.WorkspaceId, setup.Owner.Id))
            .ShouldBe(WorkspaceRole.Owner);
        (await GetMembershipRoleAsync(setup.WorkspaceId, setup.Editor.Id))
            .ShouldBe(WorkspaceRole.Editor);
    }

    [Fact]
    public async Task NonOwner_CannotTransferOwnership()
    {
        var setup = await CreateMembershipSetupAsync();

        using var response = await app.SendAsync(
            HttpMethod.Post,
            $"/api/workspaces/{setup.WorkspaceId}/ownership-transfer",
            setup.EditorToken,
            new { newOwnerUserId = setup.Member.Id });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await GetMembershipRoleAsync(setup.WorkspaceId, setup.Owner.Id))
            .ShouldBe(WorkspaceRole.Owner);
        (await GetMembershipRoleAsync(setup.WorkspaceId, setup.Member.Id))
            .ShouldBe(WorkspaceRole.Member);
    }

    [Fact]
    public async Task OwnershipTransfer_RejectsNewOwnerAtConfiguredWorkspaceLimit()
    {
        var setup = await CreateMembershipSetupAsync();
        var maximum = await app.WithServiceAsync<WorkspaceOwnershipLimitService, int>(
            service => Task.FromResult(service.MaximumOwnedWorkspaces));
        for (var index = 0; index < maximum; index++)
            await app.SeedWorkspaceAsync((setup.Editor, WorkspaceRole.Owner));

        using var response = await app.SendAsync(
            HttpMethod.Post,
            $"/api/workspaces/{setup.WorkspaceId}/ownership-transfer",
            setup.OwnerToken,
            new { newOwnerUserId = setup.Editor.Id });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ApiProblem>(
            JsonOptions,
            TestContext.Current.CancellationToken);
        problem.ShouldNotBeNull();
        problem.Code.ShouldBe("workspace_limit_reached");
        (await GetMembershipRoleAsync(setup.WorkspaceId, setup.Owner.Id))
            .ShouldBe(WorkspaceRole.Owner);
        (await GetMembershipRoleAsync(setup.WorkspaceId, setup.Editor.Id))
            .ShouldBe(WorkspaceRole.Editor);
    }

    [Fact]
    public async Task LeaveWorkspace_ReturnsNotFoundForUnknownOrUnjoinedWorkspace()
    {
        var setup = await CreateMembershipSetupAsync();

        using var unknownWorkspace = await app.SendAsync(
            HttpMethod.Delete,
            $"/api/me/workspaces/{Guid.NewGuid()}",
            setup.MemberToken);
        unknownWorkspace.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        using var unjoinedWorkspace = await app.SendAsync(
            HttpMethod.Delete,
            $"/api/me/workspaces/{setup.WorkspaceId}",
            setup.OutsiderToken);
        unjoinedWorkspace.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        (await MembershipExistsAsync(setup.WorkspaceId, setup.Member.Id)).ShouldBeTrue();
    }

    [Fact]
    public async Task ListMembers_OrdersByRoleThenEmailAcrossPages()
    {
        var setup = await CreateOrderedMembershipSetupAsync();

        var first = await GetMembersAsync(setup.WorkspaceId, setup.OwnerToken, 1, 2);
        first.Page.ShouldBe(1);
        first.PageSize.ShouldBe(2);
        first.Total.ShouldBe(5);
        first.Items.Select(member => member.Email).ShouldBe(
            new[] { "owner@example.test", "alpha-editor@example.test" });
        first.Items.Select(member => member.Role).ShouldBe(
            new[] { WorkspaceRole.Owner, WorkspaceRole.Editor });

        var second = await GetMembersAsync(setup.WorkspaceId, setup.OwnerToken, 2, 2);
        second.Items.Select(member => member.Email).ShouldBe(
            new[] { "zulu-editor@example.test", "alpha-member@example.test" });
        second.Items.Select(member => member.Role).ShouldBe(
            new[] { WorkspaceRole.Editor, WorkspaceRole.Member });

        var third = await GetMembersAsync(setup.WorkspaceId, setup.OwnerToken, 3, 2);
        third.Items.Select(member => member.Email).ShouldBe(
            new[] { "zulu-member@example.test" });

        var pastEnd = await GetMembersAsync(setup.WorkspaceId, setup.OwnerToken, 4, 2);
        pastEnd.Page.ShouldBe(4);
        pastEnd.Total.ShouldBe(5);
        pastEnd.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListMembers_NormalizesPageBoundaries()
    {
        var setup = await CreateOrderedMembershipSetupAsync();

        var minimum = await GetMembersAsync(setup.WorkspaceId, setup.OwnerToken, 0, 0);
        minimum.Page.ShouldBe(1);
        minimum.PageSize.ShouldBe(1);
        minimum.Total.ShouldBe(5);
        minimum.Items.Single().Email.ShouldBe("owner@example.test");

        var maximum = await GetMembersAsync(setup.WorkspaceId, setup.OwnerToken, -10, 101);
        maximum.Page.ShouldBe(1);
        maximum.PageSize.ShouldBe(100);
        maximum.Total.ShouldBe(5);
        maximum.Items.Count.ShouldBe(5);
    }

    private async Task<MembershipSetup> CreateMembershipSetupAsync()
    {
        const string password = "membership-password";
        var owner = await app.SeedUserAsync("owner@example.test", password);
        var editor = await app.SeedUserAsync("editor@example.test", password);
        var member = await app.SeedUserAsync("member@example.test", password);
        var outsider = await app.SeedUserAsync("outsider@example.test", password);
        var workspace = await app.SeedWorkspaceAsync(
            (owner, WorkspaceRole.Owner),
            (editor, WorkspaceRole.Editor),
            (member, WorkspaceRole.Member));

        return new MembershipSetup(
            workspace.Id,
            owner,
            editor,
            member,
            outsider,
            await app.LoginAsync(owner.Email, password),
            await app.LoginAsync(editor.Email, password),
            await app.LoginAsync(member.Email, password),
            await app.LoginAsync(outsider.Email, password));
    }

    private async Task<(Guid WorkspaceId, string OwnerToken)>
        CreateOrderedMembershipSetupAsync()
    {
        const string password = "membership-password";
        var owner = await app.SeedUserAsync("owner@example.test", password);
        var alphaEditor = await app.SeedUserAsync("alpha-editor@example.test", password);
        var zuluEditor = await app.SeedUserAsync("zulu-editor@example.test", password);
        var alphaMember = await app.SeedUserAsync("alpha-member@example.test", password);
        var zuluMember = await app.SeedUserAsync("zulu-member@example.test", password);
        var workspace = await app.SeedWorkspaceAsync(
            (zuluMember, WorkspaceRole.Member),
            (zuluEditor, WorkspaceRole.Editor),
            (owner, WorkspaceRole.Owner),
            (alphaMember, WorkspaceRole.Member),
            (alphaEditor, WorkspaceRole.Editor));

        return (workspace.Id, await app.LoginAsync(owner.Email, password));
    }

    private async Task<ListWorkspaceMembersResponse> GetMembersAsync(
        Guid workspaceId,
        string ownerToken,
        int page,
        int pageSize)
    {
        using var response = await app.SendAsync(
            HttpMethod.Get,
            $"/api/workspaces/{workspaceId}/members?page={page}&pageSize={pageSize}",
            ownerToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content
            .ReadFromJsonAsync<ListWorkspaceMembersResponse>(
                JsonOptions,
                TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        return result;
    }

    private Task<bool> MembershipExistsAsync(Guid workspaceId, string userId) =>
        app.WithDatabaseAsync(
            db => db.WorkspaceMemberships.AnyAsync(
                membership =>
                    membership.WorkspaceId == workspaceId &&
                    membership.UserId == userId));

    private Task<WorkspaceRole> GetMembershipRoleAsync(Guid workspaceId, string userId) =>
        app.WithDatabaseAsync(
            db => db.WorkspaceMemberships
                .Where(
                    membership =>
                        membership.WorkspaceId == workspaceId &&
                        membership.UserId == userId)
                .Select(membership => membership.Role)
                .SingleAsync());

    private static string MemberPath(Guid workspaceId, string userId) =>
        $"/api/workspaces/{workspaceId}/members/{userId}";

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed record MembershipSetup(
        Guid WorkspaceId,
        User Owner,
        User Editor,
        User Member,
        User Outsider,
        string OwnerToken,
        string EditorToken,
        string MemberToken,
        string OutsiderToken);
}
