using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FastEndpoints;
using FastEndpoints.Security;
using FastEndpoints.Testing;
using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Endpoints.Auth;
using HeadlessCms.Api.Endpoints.Content;
using HeadlessCms.Api.Endpoints.Projects;
using HeadlessCms.Api.Tenancy.Models;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace HeadlessCms.Api.Tests;

[Collection<TestAppCollection>]
public sealed class ContentEndpointTests(TestApp app) : TestBase
{
    protected override async ValueTask SetupAsync()
    {
        await app.ResetDatabaseAsync();
    }

    [Fact]
    public async Task Owner_CanDefineValidateAndQueryDynamicContent()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await app.SeedUserAsync("owner", "owner-password");
        var tenant = await app.SeedTenantAsync((owner, TenantRole.Owner));
        var token = await LoginAsync("owner", "owner-password");
        var project = await CreateProjectAsync(tenant.Id, token);

        var createType = await SendAsync(
            HttpMethod.Post,
            ContentTypesPath(tenant.Id, project.Id),
            token,
            ArticleDefinition());

        createType.StatusCode.ShouldBe(HttpStatusCode.Created);
        var type = await createType.Content.ReadFromJsonAsync<CreateContentTypeResponse>(
            cancellationToken: ct);
        type.ShouldNotBeNull();
        type.Key.ShouldBe("article");
        type.ProjectId.ShouldBe(project.Id);
        type.Version.ShouldBe(1);
        type.Fields.Count.ShouldBe(3);

        var first = await CreateEntryAsync(
            tenant.Id,
            project.Id,
            token,
            new { title = "First", views = 10, published = false });
        var second = await CreateEntryAsync(
            tenant.Id,
            project.Id,
            token,
            new { title = "Second", views = 125, published = true });

        first.SchemaVersion.ShouldBe(1);
        second.Data.GetProperty("views").GetInt32().ShouldBe(125);

        var invalid = await SendAsync(
            HttpMethod.Post,
            EntriesPath(tenant.Id, project.Id),
            token,
            new
            {
                data = new { title = "Invalid", views = "many", unexpected = true }
            });
        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var list = await SendAsync(
            HttpMethod.Get,
            EntriesPath(tenant.Id, project.Id) +
            "?filter[views][gte]=100&sort=-views",
            token);
        list.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await list.Content.ReadFromJsonAsync<ListContentEntriesResponse>(
            cancellationToken: ct);
        page.ShouldNotBeNull();
        page.Total.ShouldBe(1);
        page.Items.Single().Id.ShouldBe(second.Id);

        var stored = await app.WithDatabaseAsync(
            db => db.ContentEntries.AsNoTracking().ToListAsync(ct));
        stored.Count.ShouldBe(2);
        stored.ShouldAllBe(entry => entry.TenantId == tenant.Id);
        stored.ShouldAllBe(entry => entry.ProjectId == project.Id);
    }

    [Theory]
    [InlineData(TenantRole.Owner, HttpStatusCode.Created)]
    [InlineData(TenantRole.Editor, HttpStatusCode.Created)]
    [InlineData(TenantRole.Member, HttpStatusCode.Forbidden)]
    public async Task ContentWrites_UseTenantMembershipRole(
        TenantRole role,
        HttpStatusCode expected)
    {
        var user = await app.SeedUserAsync(role.ToString().ToLowerInvariant(), "password");
        var tenant = await app.SeedTenantAsync((user, role));
        var project = await SeedProjectAsync(tenant.Id);
        var token = await LoginAsync(role.ToString().ToLowerInvariant(), "password");

        var response = await SendAsync(
            HttpMethod.Post,
            ContentTypesPath(tenant.Id, project.Id),
            token,
            ArticleDefinition());

        response.StatusCode.ShouldBe(expected);
    }

    [Fact]
    public async Task Membership_DoesNotGrantAccessToAnotherTenant()
    {
        var user = await app.SeedUserAsync("editor", "password");
        await app.SeedTenantAsync((user, TenantRole.Editor));
        var otherTenant = await app.WithDatabaseAsync(
            async db =>
            {
                var tenant = new Tenant { Name = "Tenant B" };
                db.Tenants.Add(tenant);
                await db.SaveChangesAsync();
                return tenant;
            });
        var otherProject = await SeedProjectAsync(otherTenant.Id);
        var token = await LoginAsync("editor", "password");

        var create = await SendAsync(
            HttpMethod.Post,
            ContentTypesPath(otherTenant.Id, otherProject.Id),
            token,
            ArticleDefinition());
        create.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var list = await SendAsync(
            HttpMethod.Get,
            ContentTypesPath(otherTenant.Id, otherProject.Id),
            token);
        list.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ContentTypesAndEntries_AreIsolatedBetweenProjects()
    {
        var owner = await app.SeedUserAsync("owner", "password");
        var tenant = await app.SeedTenantAsync((owner, TenantRole.Owner));
        var firstProject = await SeedProjectAsync(tenant.Id);
        var secondProject = await SeedProjectAsync(tenant.Id);
        var token = await LoginAsync("owner", "password");

        var firstType = await SendAsync(
            HttpMethod.Post,
            ContentTypesPath(tenant.Id, firstProject.Id),
            token,
            ArticleDefinition());
        var secondType = await SendAsync(
            HttpMethod.Post,
            ContentTypesPath(tenant.Id, secondProject.Id),
            token,
            ArticleDefinition());

        firstType.StatusCode.ShouldBe(HttpStatusCode.Created);
        secondType.StatusCode.ShouldBe(HttpStatusCode.Created);

        var entry = await CreateEntryAsync(
            tenant.Id,
            firstProject.Id,
            token,
            new { title = "First project", views = 1, published = false });

        var crossProjectRead = await SendAsync(
            HttpMethod.Get,
            $"{EntriesPath(tenant.Id, secondProject.Id)}/{entry.Id}",
            token);
        crossProjectRead.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var storedTypes = await app.WithDatabaseAsync(
            db => db.ContentTypes
                .AsNoTracking()
                .Where(type => type.TenantId == tenant.Id)
                .ToListAsync());
        storedTypes.Count.ShouldBe(2);
        storedTypes.Select(type => type.ProjectId)
            .ShouldBe([firstProject.Id, secondProject.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task DefinitionUpdates_AreVersionedAndDoNotChangeOldEntryValidation()
    {
        var ct = TestContext.Current.CancellationToken;
        var editor = await app.SeedUserAsync("editor", "password");
        var tenant = await app.SeedTenantAsync((editor, TenantRole.Editor));
        var project = await SeedProjectAsync(tenant.Id);
        var token = await LoginAsync("editor", "password");

        await SendAsync(
            HttpMethod.Post,
            ContentTypesPath(tenant.Id, project.Id),
            token,
            ArticleDefinition());
        var oldEntry = await CreateEntryAsync(
            tenant.Id,
            project.Id,
            token,
            new { title = "Old", views = 1, published = false });

        var updateType = await SendAsync(
            HttpMethod.Put,
            ContentTypePath(tenant.Id, project.Id),
            token,
            new
            {
                name = "Article",
                fields = new object[]
                {
                    new { key = "title", name = "Title", type = "text", required = true },
                    new { key = "views", name = "Views", type = "number" },
                    new { key = "published", name = "Published", type = "boolean", required = true },
                    new { key = "summary", name = "Summary", type = "text", required = true }
                }
            });

        updateType.StatusCode.ShouldBe(HttpStatusCode.OK);
        var versionTwo = await updateType.Content.ReadFromJsonAsync<UpdateContentTypeResponse>(
            cancellationToken: ct);
        versionTwo.ShouldNotBeNull();
        versionTwo.Version.ShouldBe(2);

        var invalidNewEntry = await SendAsync(
            HttpMethod.Post,
            EntriesPath(tenant.Id, project.Id),
            token,
            new
            {
                data = new { title = "New", views = 2, published = true }
            });
        invalidNewEntry.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var updateOldEntry = await SendAsync(
            HttpMethod.Put,
            $"{EntriesPath(tenant.Id, project.Id)}/{oldEntry.Id}",
            token,
            new
            {
                data = new { title = "Still old", views = 3, published = true }
            });
        updateOldEntry.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await updateOldEntry.Content.ReadFromJsonAsync<UpdateContentEntryResponse>(
            cancellationToken: ct);
        updated.ShouldNotBeNull();
        updated.SchemaVersion.ShouldBe(1);

        var incompatible = await SendAsync(
            HttpMethod.Put,
            ContentTypePath(tenant.Id, project.Id),
            token,
            new
            {
                name = "Article",
                fields = new object[]
                {
                    new { key = "title", name = "Title", type = "number", required = true }
                }
            });
        incompatible.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private static object ArticleDefinition() =>
        new
        {
            key = "article",
            name = "Article",
            fields = new object[]
            {
                new
                {
                    key = "title",
                    name = "Title",
                    type = "text",
                    required = true
                },
                new
                {
                    key = "views",
                    name = "Views",
                    type = "number",
                    settings = new { @default = 0 }
                },
                new
                {
                    key = "published",
                    name = "Published",
                    type = "boolean",
                    required = true
                }
            }
        };

    private async Task<CreateContentEntryResponse> CreateEntryAsync(
        Guid tenantId,
        Guid projectId,
        string token,
        object data)
    {
        var response = await SendAsync(
            HttpMethod.Post,
            EntriesPath(tenantId, projectId),
            token,
            new { data });
        var responseBody = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, responseBody);
        var entry = await response.Content.ReadFromJsonAsync<CreateContentEntryResponse>(
            cancellationToken: TestContext.Current.CancellationToken);
        entry.ShouldNotBeNull();
        return entry;
    }

    private async Task<CreateProjectResponse> CreateProjectAsync(
        Guid tenantId,
        string token)
    {
        var response = await SendAsync(
            HttpMethod.Post,
            $"/api/tenants/{tenantId}/projects",
            token,
            new { name = "Website" });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var project = await response.Content.ReadFromJsonAsync<CreateProjectResponse>(
            cancellationToken: TestContext.Current.CancellationToken);
        project.ShouldNotBeNull();
        return project;
    }

    private Task<Project> SeedProjectAsync(Guid tenantId) =>
        app.WithDatabaseAsync(
            async db =>
            {
                var now = DateTime.UtcNow;
                var project = new Project
                {
                    TenantId = tenantId,
                    Name = $"Project {Guid.NewGuid():N}",
                    CreatedAt = now,
                    UpdatedAt = now
                };
                db.Projects.Add(project);
                await db.SaveChangesAsync();
                return project;
            });

    private static string ContentTypesPath(Guid tenantId, Guid projectId) =>
        $"/api/tenants/{tenantId}/projects/{projectId}/content-types";

    private static string ContentTypePath(Guid tenantId, Guid projectId) =>
        $"{ContentTypesPath(tenantId, projectId)}/article";

    private static string EntriesPath(Guid tenantId, Guid projectId) =>
        $"{ContentTypePath(tenantId, projectId)}/entries";

    private Task<string> LoginAsync(string identifier, string password) =>
        app.LoginAsync(identifier, password);

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        string accessToken,
        object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (body is not null)
            request.Content = JsonContent.Create(body);

        return await app.HttpsClient.SendAsync(
            request,
            TestContext.Current.CancellationToken);
    }
}
