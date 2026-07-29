using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FastEndpoints.Testing;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Endpoints.Projects;
using HeadlessCms.Api.Endpoints.Tenants;
using HeadlessCms.Api.Tenancy.Models;
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
    public async Task CreateTenant_TrimsNameAndNormalizesOwnerEmail()
    {
        var admin = await app.SeedUserAsync(
            "admin",
            "password",
            PlatformRole.PlatformAdmin);
        var token = await app.LoginAsync(admin.Email, "password");

        using var response = await app.SendAsync(
            HttpMethod.Post,
            "/api/tenants",
            token,
            new
            {
                name = "  Tenant A  ",
                ownerEmail = "  OWNER@EXAMPLE.TEST  "
            });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<CreateTenantResponse>(
            JsonOptions,
            TestContext.Current.CancellationToken);
        created.ShouldNotBeNull();
        created.Tenant.Name.ShouldBe("Tenant A");
        created.OwnerInvitation.Email.ShouldBe("owner@example.test");
    }

    [Theory]
    [InlineData("", "owner@example.test")]
    [InlineData("   ", "owner@example.test")]
    [InlineData(
        "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
        "owner@example.test")]
    [InlineData("Tenant A", "not-an-email")]
    public async Task CreateTenant_RejectsInvalidInputWithoutPersisting(
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
            "/api/tenants",
            token,
            new { name, ownerEmail });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var tenantCount = await app.WithDatabaseAsync(db => db.Tenants.CountAsync());
        tenantCount.ShouldBe(0);
    }

    [Fact]
    public async Task ListMyTenants_ReturnsAlphabeticalMembershipsAndRejectsAnonymous()
    {
        var user = await app.SeedUserAsync("member", "password");
        var zulu = await SeedNamedTenantAsync("Zulu", user, TenantRole.Member);
        var alpha = await SeedNamedTenantAsync("Alpha", user, TenantRole.Editor);
        var token = await app.LoginAsync(user.Email, "password");

        using var response = await app.SendAsync(
            HttpMethod.Get,
            "/api/me/tenants",
            token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var tenants = await response.Content.ReadFromJsonAsync<List<ListMyTenantsItemResponse>>(
            JsonOptions,
            TestContext.Current.CancellationToken);
        tenants.ShouldNotBeNull();
        tenants.Select(tenant => tenant.Id).ShouldBe([alpha.Id, zulu.Id]);
        tenants.Select(tenant => tenant.CurrentRole)
            .ShouldBe([TenantRole.Editor, TenantRole.Member]);

        using var anonymous = await app.HttpsClient.GetAsync(
            "/api/me/tenants",
            TestContext.Current.CancellationToken);
        anonymous.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateProject_TrimsNameAndAcceptsLengthBoundaries()
    {
        var owner = await app.SeedUserAsync("owner", "password");
        var tenant = await app.SeedTenantAsync((owner, TenantRole.Owner));
        var project = await app.SeedProjectAsync(tenant.Id, "Original");
        var token = await app.LoginAsync(owner.Email, "password");

        using var minimum = await app.SendAsync(
            HttpMethod.Put,
            TestApp.ProjectPath(tenant.Id, project.Id),
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
            TestApp.ProjectPath(tenant.Id, project.Id),
            token,
            new { name = maximumName });
        maximum.StatusCode.ShouldBe(HttpStatusCode.OK);
        var maximumResult = await maximum.Content.ReadFromJsonAsync<UpdateProjectResponse>(
            cancellationToken: TestContext.Current.CancellationToken);
        maximumResult.ShouldNotBeNull();
        maximumResult.Name.ShouldBe(maximumName);
    }

    [Fact]
    public async Task PlatformAdmin_ListingInvitationsForMissingTenantReturnsNotFound()
    {
        var admin = await app.SeedUserAsync(
            "admin",
            "password",
            PlatformRole.PlatformAdmin);
        var token = await app.LoginAsync(admin.Email, "password");

        using var response = await app.SendAsync(
            HttpMethod.Get,
            $"/api/tenants/{Guid.NewGuid()}/invitations",
            token);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private Task<Tenant> SeedNamedTenantAsync(
        string name,
        User user,
        TenantRole role) =>
        app.WithDatabaseAsync(
            async db =>
            {
                var tenant = new Tenant
                {
                    Name = name,
                    Memberships =
                    [
                        new TenantMembership
                        {
                            UserId = user.Id,
                            Role = role
                        }
                    ]
                };
                db.Tenants.Add(tenant);
                await db.SaveChangesAsync();
                return tenant;
            });

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
