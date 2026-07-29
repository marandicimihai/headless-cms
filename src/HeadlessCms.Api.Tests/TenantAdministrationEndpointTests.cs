using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FastEndpoints.Testing;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Endpoints.Tenants;
using HeadlessCms.Api.Tenancy.Models;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace HeadlessCms.Api.Tests;

[Collection<TestAppCollection>]
public sealed class TenantAdministrationEndpointTests(TestApp app) : TestBase
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    protected override async ValueTask SetupAsync() => await app.ResetDatabaseAsync();

    [Fact]
    public async Task ListTenants_PlatformAdminGetsRequestedPageInNameOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        var admin = await app.SeedUserAsync(
            "admin",
            "admin-password",
            PlatformRole.PlatformAdmin);
        await SeedTenantAsync("Zulu");
        await SeedTenantAsync("Bravo");
        await SeedTenantAsync("Echo");
        await SeedTenantAsync("Alpha");
        var accessToken = await app.LoginAsync(admin.Email, "admin-password");

        using var response = await app.SendAsync(
            HttpMethod.Get,
            "/api/tenants?page=2&pageSize=2",
            accessToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<TenantResponse>>(
            JsonOptions,
            cancellationToken: ct);
        page.ShouldNotBeNull();
        page.Page.ShouldBe(2);
        page.PageSize.ShouldBe(2);
        page.Total.ShouldBe(4);
        page.Items.Select(tenant => tenant.Name).ShouldBe(["Echo", "Zulu"]);
        page.Items.ShouldAllBe(tenant => tenant.CurrentRole == null);
    }

    [Fact]
    public async Task ListTenants_NormalizesPaginationBoundsAndReturnsEmptyOutOfRangePage()
    {
        var ct = TestContext.Current.CancellationToken;
        var admin = await app.SeedUserAsync(
            "admin",
            "admin-password",
            PlatformRole.PlatformAdmin);
        await SeedTenantAsync("Bravo");
        await SeedTenantAsync("Alpha");
        var accessToken = await app.LoginAsync(admin.Email, "admin-password");

        using var minimumResponse = await app.SendAsync(
            HttpMethod.Get,
            "/api/tenants?page=0&pageSize=0",
            accessToken);
        minimumResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var minimumPage = await minimumResponse.Content
            .ReadFromJsonAsync<PagedResponse<TenantResponse>>(
                JsonOptions,
                cancellationToken: ct);
        minimumPage.ShouldNotBeNull();
        minimumPage.Page.ShouldBe(1);
        minimumPage.PageSize.ShouldBe(1);
        minimumPage.Total.ShouldBe(2);
        minimumPage.Items.Select(tenant => tenant.Name).ShouldBe(["Alpha"]);

        using var maximumResponse = await app.SendAsync(
            HttpMethod.Get,
            "/api/tenants?page=1&pageSize=101",
            accessToken);
        maximumResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var maximumPage = await maximumResponse.Content
            .ReadFromJsonAsync<PagedResponse<TenantResponse>>(
                JsonOptions,
                cancellationToken: ct);
        maximumPage.ShouldNotBeNull();
        maximumPage.Page.ShouldBe(1);
        maximumPage.PageSize.ShouldBe(100);
        maximumPage.Total.ShouldBe(2);
        maximumPage.Items.Select(tenant => tenant.Name).ShouldBe(["Alpha", "Bravo"]);

        using var outOfRangeResponse = await app.SendAsync(
            HttpMethod.Get,
            "/api/tenants?page=3&pageSize=1",
            accessToken);
        outOfRangeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var outOfRangePage = await outOfRangeResponse.Content
            .ReadFromJsonAsync<PagedResponse<TenantResponse>>(
                JsonOptions,
                cancellationToken: ct);
        outOfRangePage.ShouldNotBeNull();
        outOfRangePage.Page.ShouldBe(3);
        outOfRangePage.PageSize.ShouldBe(1);
        outOfRangePage.Total.ShouldBe(2);
        outOfRangePage.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListTenants_RejectsTenantMemberAndOutsider()
    {
        var member = await app.SeedUserAsync("member", "member-password");
        var outsider = await app.SeedUserAsync("outsider", "outsider-password");
        await SeedTenantAsync("Private tenant", (member, TenantRole.Member));
        var memberToken = await app.LoginAsync(member.Email, "member-password");
        var outsiderToken = await app.LoginAsync(outsider.Email, "outsider-password");

        using var memberResponse = await app.SendAsync(
            HttpMethod.Get,
            "/api/tenants",
            memberToken);
        memberResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var outsiderResponse = await app.SendAsync(
            HttpMethod.Get,
            "/api/tenants",
            outsiderToken);
        outsiderResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetTenant_ReturnsTenantToPlatformAdminAndMembersWithTheirCurrentRole()
    {
        var ct = TestContext.Current.CancellationToken;
        var admin = await app.SeedUserAsync(
            "admin",
            "admin-password",
            PlatformRole.PlatformAdmin);
        var owner = await app.SeedUserAsync("owner", "owner-password");
        var editor = await app.SeedUserAsync("editor", "editor-password");
        var member = await app.SeedUserAsync("member", "member-password");
        var tenant = await SeedTenantAsync(
            "Visible tenant",
            (owner, TenantRole.Owner),
            (editor, TenantRole.Editor),
            (member, TenantRole.Member));

        var callers = new[]
        {
            (User: admin, Password: "admin-password", ExpectedRole: (TenantRole?)null),
            (User: owner, Password: "owner-password", ExpectedRole: (TenantRole?)TenantRole.Owner),
            (User: editor, Password: "editor-password", ExpectedRole: (TenantRole?)TenantRole.Editor),
            (User: member, Password: "member-password", ExpectedRole: (TenantRole?)TenantRole.Member)
        };

        foreach (var caller in callers)
        {
            var accessToken = await app.LoginAsync(caller.User.Email, caller.Password);
            using var response = await app.SendAsync(
                HttpMethod.Get,
                TenantPath(tenant.Id),
                accessToken);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var returnedTenant = await response.Content.ReadFromJsonAsync<TenantResponse>(
                JsonOptions,
                cancellationToken: ct);
            returnedTenant.ShouldNotBeNull();
            returnedTenant.Id.ShouldBe(tenant.Id);
            returnedTenant.Name.ShouldBe("Visible tenant");
            returnedTenant.CreatedAt.ShouldBe(
                tenant.CreatedAt,
                TimeSpan.FromMilliseconds(1));
            returnedTenant.CurrentRole.ShouldBe(caller.ExpectedRole);
        }
    }

    [Fact]
    public async Task GetTenant_HidesExistingTenantFromOutsiderAndReturnsNotFoundForUnknownTenant()
    {
        var admin = await app.SeedUserAsync(
            "admin",
            "admin-password",
            PlatformRole.PlatformAdmin);
        var member = await app.SeedUserAsync("member", "member-password");
        var outsider = await app.SeedUserAsync("outsider", "outsider-password");
        var tenant = await SeedTenantAsync(
            "Private tenant",
            (member, TenantRole.Member));
        var adminToken = await app.LoginAsync(admin.Email, "admin-password");
        var memberToken = await app.LoginAsync(member.Email, "member-password");
        var outsiderToken = await app.LoginAsync(outsider.Email, "outsider-password");
        var missingTenantId = Guid.NewGuid();

        using var outsiderResponse = await app.SendAsync(
            HttpMethod.Get,
            TenantPath(tenant.Id),
            outsiderToken);
        outsiderResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        using var adminResponse = await app.SendAsync(
            HttpMethod.Get,
            TenantPath(missingTenantId),
            adminToken);
        adminResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        using var memberResponse = await app.SendAsync(
            HttpMethod.Get,
            TenantPath(missingTenantId),
            memberToken);
        memberResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RenameTenant_PlatformAdminAndOwnerCanRenameAndNamesAreTrimmed()
    {
        var ct = TestContext.Current.CancellationToken;
        var admin = await app.SeedUserAsync(
            "admin",
            "admin-password",
            PlatformRole.PlatformAdmin);
        var owner = await app.SeedUserAsync("owner", "owner-password");
        var tenant = await SeedTenantAsync(
            "Original tenant",
            (owner, TenantRole.Owner));
        var adminToken = await app.LoginAsync(admin.Email, "admin-password");
        var ownerToken = await app.LoginAsync(owner.Email, "owner-password");

        using var adminResponse = await app.SendAsync(
            HttpMethod.Patch,
            TenantPath(tenant.Id),
            adminToken,
            new { name = "  Admin renamed  " });
        adminResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var adminResult = await adminResponse.Content.ReadFromJsonAsync<TenantResponse>(
            JsonOptions,
            cancellationToken: ct);
        adminResult.ShouldNotBeNull();
        adminResult.Name.ShouldBe("Admin renamed");
        adminResult.CurrentRole.ShouldBeNull();

        using var ownerResponse = await app.SendAsync(
            HttpMethod.Patch,
            TenantPath(tenant.Id),
            ownerToken,
            new { name = "  Owner renamed  " });
        ownerResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var ownerResult = await ownerResponse.Content.ReadFromJsonAsync<TenantResponse>(
            JsonOptions,
            cancellationToken: ct);
        ownerResult.ShouldNotBeNull();
        ownerResult.Name.ShouldBe("Owner renamed");
        ownerResult.CurrentRole.ShouldBe(TenantRole.Owner);

        var storedName = await app.WithDatabaseAsync(
            db => db.Tenants
                .Where(candidate => candidate.Id == tenant.Id)
                .Select(candidate => candidate.Name)
                .SingleAsync(ct));
        storedName.ShouldBe("Owner renamed");
    }

    [Fact]
    public async Task RenameTenant_RejectsEditorMemberAndOutsiderWithoutChangingTenant()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await app.SeedUserAsync("owner", "owner-password");
        var editor = await app.SeedUserAsync("editor", "editor-password");
        var member = await app.SeedUserAsync("member", "member-password");
        var outsider = await app.SeedUserAsync("outsider", "outsider-password");
        var tenant = await SeedTenantAsync(
            "Original tenant",
            (owner, TenantRole.Owner),
            (editor, TenantRole.Editor),
            (member, TenantRole.Member));

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
                TenantPath(tenant.Id),
                accessToken,
                new { name = $"Renamed by {caller.User.Email}" });
            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        var storedName = await app.WithDatabaseAsync(
            db => db.Tenants
                .Where(candidate => candidate.Id == tenant.Id)
                .Select(candidate => candidate.Name)
                .SingleAsync(ct));
        storedName.ShouldBe("Original tenant");
    }

    [Fact]
    public async Task RenameTenant_ReturnsNotFoundForUnknownTenant()
    {
        var admin = await app.SeedUserAsync(
            "admin",
            "admin-password",
            PlatformRole.PlatformAdmin);
        var owner = await app.SeedUserAsync("owner", "owner-password");
        await SeedTenantAsync("Owned tenant", (owner, TenantRole.Owner));
        var adminToken = await app.LoginAsync(admin.Email, "admin-password");
        var ownerToken = await app.LoginAsync(owner.Email, "owner-password");
        var missingTenantPath = TenantPath(Guid.NewGuid());

        using var adminResponse = await app.SendAsync(
            HttpMethod.Patch,
            missingTenantPath,
            adminToken,
            new { name = "Missing tenant" });
        adminResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        using var ownerResponse = await app.SendAsync(
            HttpMethod.Patch,
            missingTenantPath,
            ownerToken,
            new { name = "Missing tenant" });
        ownerResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RenameTenant_ValidatesNameBoundariesAndPreservesPersistedValue()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await app.SeedUserAsync("owner", "owner-password");
        var tenant = await SeedTenantAsync(
            "Original tenant",
            (owner, TenantRole.Owner));
        var ownerToken = await app.LoginAsync(owner.Email, "owner-password");

        foreach (var invalidName in new[] { "", "   ", new string('x', 101) })
        {
            using var response = await app.SendAsync(
                HttpMethod.Patch,
                TenantPath(tenant.Id),
                ownerToken,
                new { name = invalidName });
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        var nameAfterInvalidRequests = await app.WithDatabaseAsync(
            db => db.Tenants
                .Where(candidate => candidate.Id == tenant.Id)
                .Select(candidate => candidate.Name)
                .SingleAsync(ct));
        nameAfterInvalidRequests.ShouldBe("Original tenant");

        var maximumLengthName = new string('x', 100);
        using var boundaryResponse = await app.SendAsync(
            HttpMethod.Patch,
            TenantPath(tenant.Id),
            ownerToken,
            new { name = maximumLengthName });
        boundaryResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var boundaryResult = await boundaryResponse.Content.ReadFromJsonAsync<TenantResponse>(
            JsonOptions,
            cancellationToken: ct);
        boundaryResult.ShouldNotBeNull();
        boundaryResult.Name.ShouldBe(maximumLengthName);

        var storedBoundaryName = await app.WithDatabaseAsync(
            db => db.Tenants
                .Where(candidate => candidate.Id == tenant.Id)
                .Select(candidate => candidate.Name)
                .SingleAsync(ct));
        storedBoundaryName.ShouldBe(maximumLengthName);
    }

    [Theory]
    [InlineData("GET", "/api/tenants", false)]
    [InlineData("GET", "/api/tenants/00000000-0000-0000-0000-000000000001", false)]
    [InlineData("PATCH", "/api/tenants/00000000-0000-0000-0000-000000000001", true)]
    public async Task TenantAdministrationEndpoints_RejectAnonymousRequests(
        string method,
        string path,
        bool includeBody)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (includeBody)
            request.Content = JsonContent.Create(new { name = "Private tenant" });

        using var response = await app.HttpsClient.SendAsync(
            request,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private async Task<Tenant> SeedTenantAsync(
        string name,
        params (User User, TenantRole Role)[] members)
    {
        return await app.WithDatabaseAsync(
            async db =>
            {
                var tenant = new Tenant
                {
                    Name = name,
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

    private static string TenantPath(Guid tenantId) => $"/api/tenants/{tenantId}";

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
