using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FastEndpoints.Testing;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Endpoints;
using HeadlessCms.Api.Endpoints.Tenants;
using HeadlessCms.Api.Tenancy.Models;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace HeadlessCms.Api.Tests;

[Collection<TestAppCollection>]
public sealed class TenantMembershipEndpointTests(TestApp app) : TestBase
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    protected override async ValueTask SetupAsync() => await app.ResetDatabaseAsync();

    [Fact]
    public async Task Owner_CanRemoveMember()
    {
        var setup = await CreateMembershipSetupAsync();
        var ct = TestContext.Current.CancellationToken;

        using var response = await app.SendAsync(
            HttpMethod.Delete,
            MemberPath(setup.TenantId, setup.Member.Id),
            setup.OwnerToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var membershipExists = await app.WithDatabaseAsync(
            db => db.TenantMemberships.AnyAsync(
                membership =>
                    membership.TenantId == setup.TenantId &&
                    membership.UserId == setup.Member.Id));
        membershipExists.ShouldBeFalse();

        using var removedMemberTenants = await app.SendAsync(
            HttpMethod.Get,
            "/api/me/tenants",
            setup.MemberToken);
        var tenants = await removedMemberTenants.Content
            .ReadFromJsonAsync<IReadOnlyList<ListMyTenantsItemResponse>>(JsonOptions, ct);
        tenants.ShouldNotBeNull();
        tenants.ShouldBeEmpty();
    }

    [Fact]
    public async Task NonOwner_CannotRemoveMember()
    {
        var setup = await CreateMembershipSetupAsync();

        using var response = await app.SendAsync(
            HttpMethod.Delete,
            MemberPath(setup.TenantId, setup.Member.Id),
            setup.EditorToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await MembershipExistsAsync(setup.TenantId, setup.Member.Id)).ShouldBeTrue();
    }

    [Fact]
    public async Task Owner_CannotRemoveOwnerOrUnknownMember()
    {
        var setup = await CreateMembershipSetupAsync();

        using var removeOwner = await app.SendAsync(
            HttpMethod.Delete,
            MemberPath(setup.TenantId, setup.Owner.Id),
            setup.OwnerToken);
        removeOwner.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        using var removeUnknown = await app.SendAsync(
            HttpMethod.Delete,
            MemberPath(setup.TenantId, setup.Outsider.Id),
            setup.OwnerToken);
        removeUnknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var memberships = await app.WithDatabaseAsync(
            db => db.TenantMemberships
                .AsNoTracking()
                .Where(membership => membership.TenantId == setup.TenantId)
                .ToDictionaryAsync(membership => membership.UserId, membership => membership.Role));
        memberships.Count.ShouldBe(3);
        memberships[setup.Owner.Id].ShouldBe(TenantRole.Owner);
    }

    [Fact]
    public async Task ChangeRole_RejectsUnsupportedRoles()
    {
        var setup = await CreateMembershipSetupAsync();
        var ct = TestContext.Current.CancellationToken;
        object[] unsupportedRoles = [TenantRole.Owner, (TenantRole)999];

        foreach (var role in unsupportedRoles)
        {
            using var response = await app.SendAsync(
                HttpMethod.Patch,
                MemberPath(setup.TenantId, setup.Member.Id),
                setup.OwnerToken,
                new { role });

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            var problem = await response.Content.ReadFromJsonAsync<ApiProblem>(JsonOptions, ct);
            problem.ShouldNotBeNull();
            problem.Code.ShouldBe("invalid_role");
        }

        (await GetMembershipRoleAsync(setup.TenantId, setup.Member.Id))
            .ShouldBe(TenantRole.Member);
    }

    [Fact]
    public async Task NonOwner_CannotChangeMemberRole()
    {
        var setup = await CreateMembershipSetupAsync();

        using var response = await app.SendAsync(
            HttpMethod.Patch,
            MemberPath(setup.TenantId, setup.Member.Id),
            setup.EditorToken,
            new { role = TenantRole.Editor });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await GetMembershipRoleAsync(setup.TenantId, setup.Member.Id))
            .ShouldBe(TenantRole.Member);
    }

    [Fact]
    public async Task ChangeRole_ReturnsNotFoundForMissingMember()
    {
        var setup = await CreateMembershipSetupAsync();

        using var response = await app.SendAsync(
            HttpMethod.Patch,
            MemberPath(setup.TenantId, setup.Outsider.Id),
            setup.OwnerToken,
            new { role = TenantRole.Editor });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Owner_CannotChangeOwnerRoleThroughMemberEndpoint()
    {
        var setup = await CreateMembershipSetupAsync();

        using var response = await app.SendAsync(
            HttpMethod.Patch,
            MemberPath(setup.TenantId, setup.Owner.Id),
            setup.OwnerToken,
            new { role = TenantRole.Member });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await GetMembershipRoleAsync(setup.TenantId, setup.Owner.Id))
            .ShouldBe(TenantRole.Owner);
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
                $"/api/tenants/{setup.TenantId}/ownership-transfer",
                setup.OwnerToken,
                new { newOwnerUserId = targetId });
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        (await GetMembershipRoleAsync(setup.TenantId, setup.Owner.Id))
            .ShouldBe(TenantRole.Owner);
        (await GetMembershipRoleAsync(setup.TenantId, setup.Editor.Id))
            .ShouldBe(TenantRole.Editor);
    }

    [Fact]
    public async Task NonOwner_CannotTransferOwnership()
    {
        var setup = await CreateMembershipSetupAsync();

        using var response = await app.SendAsync(
            HttpMethod.Post,
            $"/api/tenants/{setup.TenantId}/ownership-transfer",
            setup.EditorToken,
            new { newOwnerUserId = setup.Member.Id });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await GetMembershipRoleAsync(setup.TenantId, setup.Owner.Id))
            .ShouldBe(TenantRole.Owner);
        (await GetMembershipRoleAsync(setup.TenantId, setup.Member.Id))
            .ShouldBe(TenantRole.Member);
    }

    [Fact]
    public async Task LeaveTenant_ReturnsNotFoundForUnknownOrUnjoinedTenant()
    {
        var setup = await CreateMembershipSetupAsync();

        using var unknownTenant = await app.SendAsync(
            HttpMethod.Delete,
            $"/api/me/tenants/{Guid.NewGuid()}",
            setup.MemberToken);
        unknownTenant.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        using var unjoinedTenant = await app.SendAsync(
            HttpMethod.Delete,
            $"/api/me/tenants/{setup.TenantId}",
            setup.OutsiderToken);
        unjoinedTenant.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        (await MembershipExistsAsync(setup.TenantId, setup.Member.Id)).ShouldBeTrue();
    }

    [Fact]
    public async Task ListMembers_OrdersByRoleThenEmailAcrossPages()
    {
        var setup = await CreateOrderedMembershipSetupAsync();

        var first = await GetMembersAsync(setup.TenantId, setup.OwnerToken, 1, 2);
        first.Page.ShouldBe(1);
        first.PageSize.ShouldBe(2);
        first.Total.ShouldBe(5);
        first.Items.Select(member => member.Email).ShouldBe(
            new[] { "owner@example.test", "alpha-editor@example.test" });
        first.Items.Select(member => member.Role).ShouldBe(
            new[] { TenantRole.Owner, TenantRole.Editor });

        var second = await GetMembersAsync(setup.TenantId, setup.OwnerToken, 2, 2);
        second.Items.Select(member => member.Email).ShouldBe(
            new[] { "zulu-editor@example.test", "alpha-member@example.test" });
        second.Items.Select(member => member.Role).ShouldBe(
            new[] { TenantRole.Editor, TenantRole.Member });

        var third = await GetMembersAsync(setup.TenantId, setup.OwnerToken, 3, 2);
        third.Items.Select(member => member.Email).ShouldBe(
            new[] { "zulu-member@example.test" });

        var pastEnd = await GetMembersAsync(setup.TenantId, setup.OwnerToken, 4, 2);
        pastEnd.Page.ShouldBe(4);
        pastEnd.Total.ShouldBe(5);
        pastEnd.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListMembers_NormalizesPageBoundaries()
    {
        var setup = await CreateOrderedMembershipSetupAsync();

        var minimum = await GetMembersAsync(setup.TenantId, setup.OwnerToken, 0, 0);
        minimum.Page.ShouldBe(1);
        minimum.PageSize.ShouldBe(1);
        minimum.Total.ShouldBe(5);
        minimum.Items.Single().Email.ShouldBe("owner@example.test");

        var maximum = await GetMembersAsync(setup.TenantId, setup.OwnerToken, -10, 101);
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
        var tenant = await app.SeedTenantAsync(
            (owner, TenantRole.Owner),
            (editor, TenantRole.Editor),
            (member, TenantRole.Member));

        return new MembershipSetup(
            tenant.Id,
            owner,
            editor,
            member,
            outsider,
            await app.LoginAsync(owner.Email, password),
            await app.LoginAsync(editor.Email, password),
            await app.LoginAsync(member.Email, password),
            await app.LoginAsync(outsider.Email, password));
    }

    private async Task<(Guid TenantId, string OwnerToken)>
        CreateOrderedMembershipSetupAsync()
    {
        const string password = "membership-password";
        var owner = await app.SeedUserAsync("owner@example.test", password);
        var alphaEditor = await app.SeedUserAsync("alpha-editor@example.test", password);
        var zuluEditor = await app.SeedUserAsync("zulu-editor@example.test", password);
        var alphaMember = await app.SeedUserAsync("alpha-member@example.test", password);
        var zuluMember = await app.SeedUserAsync("zulu-member@example.test", password);
        var tenant = await app.SeedTenantAsync(
            (zuluMember, TenantRole.Member),
            (zuluEditor, TenantRole.Editor),
            (owner, TenantRole.Owner),
            (alphaMember, TenantRole.Member),
            (alphaEditor, TenantRole.Editor));

        return (tenant.Id, await app.LoginAsync(owner.Email, password));
    }

    private async Task<ListTenantMembersResponse> GetMembersAsync(
        Guid tenantId,
        string ownerToken,
        int page,
        int pageSize)
    {
        using var response = await app.SendAsync(
            HttpMethod.Get,
            $"/api/tenants/{tenantId}/members?page={page}&pageSize={pageSize}",
            ownerToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content
            .ReadFromJsonAsync<ListTenantMembersResponse>(
                JsonOptions,
                TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        return result;
    }

    private Task<bool> MembershipExistsAsync(Guid tenantId, string userId) =>
        app.WithDatabaseAsync(
            db => db.TenantMemberships.AnyAsync(
                membership =>
                    membership.TenantId == tenantId &&
                    membership.UserId == userId));

    private Task<TenantRole> GetMembershipRoleAsync(Guid tenantId, string userId) =>
        app.WithDatabaseAsync(
            db => db.TenantMemberships
                .Where(
                    membership =>
                        membership.TenantId == tenantId &&
                        membership.UserId == userId)
                .Select(membership => membership.Role)
                .SingleAsync());

    private static string MemberPath(Guid tenantId, string userId) =>
        $"/api/tenants/{tenantId}/members/{userId}";

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed record MembershipSetup(
        Guid TenantId,
        User Owner,
        User Editor,
        User Member,
        User Outsider,
        string OwnerToken,
        string EditorToken,
        string MemberToken,
        string OutsiderToken);
}
