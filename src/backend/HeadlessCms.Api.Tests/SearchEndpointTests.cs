using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FastEndpoints.Testing;
using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Content.FieldTypes;
using HeadlessCms.Api.Content.Services;
using Microsoft.EntityFrameworkCore;
using HeadlessCms.Api.Endpoints.Search;
using HeadlessCms.Api.Workspaces.Models;
using Shouldly;
using Xunit;

namespace HeadlessCms.Api.Tests;

[Collection<TestAppCollection>]
public sealed class SearchEndpointTests(TestApp app) : TestBase
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            Converters =
            {
                new JsonStringEnumConverter(
                    JsonNamingPolicy.CamelCase,
                    allowIntegerValues: false)
            }
        };

    protected override async ValueTask SetupAsync()
    {
        await app.ResetDatabaseAsync();
    }

    [Theory]
    [InlineData(ContentFieldType.Text, "title", "contains", "Article", 2)]
    [InlineData(ContentFieldType.Number, "views", "gte", "456", 2)]
    [InlineData(ContentFieldType.Boolean, "featured", "eq", "true", 1)]
    public async Task PostgreSql_HandlerQueriesExecuteOnServer(
        ContentFieldType type, string key, string operation, string value, int expectedCount)
    {
        var owner = await app.SeedUserAsync("owner", "owner-password");
        var workspace = await app.SeedWorkspaceAsync((owner, WorkspaceRole.Owner));
        await SeedSearchDataAsync(workspace.Id);
        await app.WithDatabaseAsync(async db =>
        {
            var registry = new ContentFieldTypeRegistry(
                [new TextFieldTypeHandler(), new NumberFieldTypeHandler(), new BooleanFieldTypeHandler()]);
            var handler = registry.Get(type);
            var query = db.ContentEntries.AsNoTracking().Where(entry => entry.WorkspaceId == workspace.Id);
            var filtered = handler.ApplyFilter(query, key, operation, value);
            (await filtered.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(expectedCount);
            var ascending = await handler.ApplySort(query, key, false).ThenBy(entry => entry.Id)
                .Select(entry => entry.Id).ToArrayAsync(TestContext.Current.CancellationToken);
            var descending = await handler.ApplySort(query, key, true).ThenByDescending(entry => entry.Id)
                .Select(entry => entry.Id).ToArrayAsync(TestContext.Current.CancellationToken);
            descending.ShouldBe(ascending.Reverse().ToArray());
            return ascending;
        });
    }

    [Fact]
    public async Task PostgreSql_SearchExcludesDisabledFieldTypes()
    {
        var owner = await app.SeedUserAsync("owner", "owner-password");
        var workspace = await app.SeedWorkspaceAsync((owner, WorkspaceRole.Owner));
        await SeedSearchDataAsync(workspace.Id);
        await app.WithDatabaseAsync(async db =>
        {
            var enabled = new ContentFieldTypeRegistry(
                [new TextFieldTypeHandler()]);
            var disabled = new ContentFieldTypeRegistry([]);
            var enabledResult = await new WorkspaceSearchService(db, enabled)
                .SearchAsync(workspace.Id, "article", 5, TestContext.Current.CancellationToken);
            enabledResult.Entries.Total.ShouldBe(3);
            var disabledResult = await new WorkspaceSearchService(db, disabled)
                .SearchAsync(workspace.Id, "article", 5, TestContext.Current.CancellationToken);
            disabledResult.Entries.Total.ShouldBe(0);
            disabledResult.Projects.Total.ShouldBe(enabledResult.Projects.Total);
            return disabledResult;
        });
    }

    [Fact]
    public async Task PostgreSql_SearchesWorkspaceObjectsRanksResultsAndReturnsTextEntrySnippets()
    {
        var owner = await app.SeedUserAsync("owner", "owner-password");
        var member = await app.SeedUserAsync("member", "member-password");
        var outsider = await app.SeedUserAsync("outsider", "outsider-password");
        var workspace = await app.SeedWorkspaceAsync(
            (owner, WorkspaceRole.Owner),
            (member, WorkspaceRole.Member));
        var ownerCookie = await app.LoginAsync("owner", "owner-password");
        var memberCookie = await app.LoginAsync("member", "member-password");
        var outsiderCookie = await app.LoginAsync("outsider", "outsider-password");
        var seeded = await SeedSearchDataAsync(workspace.Id);

        var response = await app.SendAsync(
            HttpMethod.Get,
            SearchPath(workspace.Id, "article", limit: 2),
            ownerCookie);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SearchWorkspaceResponse>(
            JsonOptions,
            cancellationToken: TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();

        body.Projects.Total.ShouldBe(3);
        body.Projects.Items.Select(project => project.Name).ShouldBe(
            ["Article catalog", "Article reference"]);
        body.ContentTypes.Total.ShouldBe(3);
        body.ContentTypes.Items.Select(contentType => contentType.Key).ShouldBe(
            ["article", "articles"]);
        body.Entries.Total.ShouldBe(3);
        body.Entries.Items.Count.ShouldBe(2);
        body.Entries.Items[0].Id.ShouldBe(seeded.ExactEntryId);
        body.Entries.Items[0].MatchedFieldKey.ShouldBe("title");
        body.Entries.Items[0].Status.ShouldBe(ContentEntryStatus.Published);
        body.Entries.Items[1].Status.ShouldBe(ContentEntryStatus.Draft);

        var allEntries = await app.SendAsync(
            HttpMethod.Get,
            SearchPath(workspace.Id, "article", limit: 5),
            memberCookie);
        var memberBody = await allEntries.Content.ReadFromJsonAsync<SearchWorkspaceResponse>(
            JsonOptions,
            cancellationToken: TestContext.Current.CancellationToken);
        memberBody.ShouldNotBeNull();
        memberBody.Entries.Items.Count.ShouldBe(3);
        memberBody.Entries.Items.Single(entry => entry.Id == seeded.LongEntryId)
            .Snippet.ShouldStartWith("…");
        memberBody.Entries.Items.Single(entry => entry.Id == seeded.LongEntryId)
            .Snippet.ShouldEndWith("…");

        var numericalSearch = await app.SendAsync(
            HttpMethod.Get,
            SearchPath(workspace.Id, "123", limit: 5),
            ownerCookie);
        var numericalBody = await numericalSearch.Content.ReadFromJsonAsync<SearchWorkspaceResponse>(
            JsonOptions,
            cancellationToken: TestContext.Current.CancellationToken);
        numericalBody.ShouldNotBeNull();
        numericalBody.Entries.Total.ShouldBe(0);

        var wildcardSearch = await app.SendAsync(
            HttpMethod.Get,
            SearchPath(workspace.Id, "%%", limit: 5),
            ownerCookie);
        var wildcardBody = await wildcardSearch.Content.ReadFromJsonAsync<SearchWorkspaceResponse>(
            JsonOptions,
            cancellationToken: TestContext.Current.CancellationToken);
        wildcardBody.ShouldNotBeNull();
        wildcardBody.Projects.Total.ShouldBe(0);
        wildcardBody.ContentTypes.Total.ShouldBe(0);
        wildcardBody.Entries.Total.ShouldBe(0);

        var inaccessible = await app.SendAsync(
            HttpMethod.Get,
            SearchPath(workspace.Id, "article", limit: 5),
            outsiderCookie);
        inaccessible.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("a", 5)]
    [InlineData(" ", 5)]
    [InlineData("article", 0)]
    [InlineData("article", 11)]
    public async Task Search_ValidatesQueryAndLimit(string query, int limit)
    {
        var owner = await app.SeedUserAsync("owner", "owner-password");
        var workspace = await app.SeedWorkspaceAsync((owner, WorkspaceRole.Owner));
        var cookie = await app.LoginAsync("owner", "owner-password");

        var response = await app.SendAsync(
            HttpMethod.Get,
            SearchPath(workspace.Id, query, limit),
            cookie);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private Task<SeededSearchData> SeedSearchDataAsync(Guid workspaceId) =>
        app.WithDatabaseAsync(async db =>
        {
            var now = DateTime.UtcNow;
            var exactProject = new Project
            {
                WorkspaceId = workspaceId,
                Name = "Article catalog",
                CreatedAt = now,
                UpdatedAt = now
            };
            var prefixProject = new Project
            {
                WorkspaceId = workspaceId,
                Name = "Article reference",
                CreatedAt = now,
                UpdatedAt = now
            };
            var substringProject = new Project
            {
                WorkspaceId = workspaceId,
                Name = "My article archive",
                CreatedAt = now,
                UpdatedAt = now
            };
            db.Projects.AddRange(exactProject, prefixProject, substringProject);
            await db.SaveChangesAsync();

            var article = CreateContentType(workspaceId, exactProject.Id, "article", now);
            var articles = CreateContentType(workspaceId, prefixProject.Id, "articles", now);
            var newsArticle = CreateContentType(workspaceId, substringProject.Id, "news_article", now);
            db.ContentTypes.AddRange(article, articles, newsArticle);
            await db.SaveChangesAsync();

            var exactEntry = CreateEntry(
                workspaceId,
                exactProject.Id,
                article.Id,
                new { title = "Article", body = "Article body", views = 123, featured = true },
                ContentEntryStatus.Published,
                now.AddMinutes(-1));
            var draftEntry = CreateEntry(
                workspaceId,
                prefixProject.Id,
                articles.Id,
                new { title = "Article draft", body = "Unrelated", views = 456, featured = false },
                ContentEntryStatus.Draft,
                now.AddMinutes(-2));
            var longEntry = CreateEntry(
                workspaceId,
                substringProject.Id,
                newsArticle.Id,
                new
                {
                    title = new string('x', 80) + " article " + new string('y', 100),
                    body = "Unrelated",
                    views = 789,
                    featured = false
                },
                ContentEntryStatus.Published,
                now.AddMinutes(-3));
            db.ContentEntries.AddRange(exactEntry, draftEntry, longEntry);
            await db.SaveChangesAsync();

            return new SeededSearchData(exactEntry.Id, longEntry.Id);
        });

    private static ContentType CreateContentType(
        Guid workspaceId,
        Guid projectId,
        string key,
        DateTime now)
    {
        var contentType = new ContentType
        {
            WorkspaceId = workspaceId,
            ProjectId = projectId,
            Key = key,
            CreatedAt = now,
            UpdatedAt = now
        };
        contentType.Fields =
        [
            CreateField(workspaceId, projectId, "title", ContentFieldType.Text, 0),
            CreateField(workspaceId, projectId, "body", ContentFieldType.Text, 1),
            CreateField(workspaceId, projectId, "views", ContentFieldType.Number, 2),
            CreateField(workspaceId, projectId, "featured", ContentFieldType.Boolean, 3)
        ];
        return contentType;
    }

    private static ContentField CreateField(
        Guid workspaceId,
        Guid projectId,
        string key,
        ContentFieldType type,
        int position) =>
        new()
        {
            WorkspaceId = workspaceId,
            ProjectId = projectId,
            Key = key,
            Type = type,
            Position = position,
            Settings = JsonDocument.Parse("{}").RootElement.Clone()
        };

    private static ContentEntry CreateEntry(
        Guid workspaceId,
        Guid projectId,
        Guid contentTypeId,
        object data,
        ContentEntryStatus status,
        DateTime updatedAt) =>
        new()
        {
            WorkspaceId = workspaceId,
            ProjectId = projectId,
            ContentTypeId = contentTypeId,
            Data = JsonDocument.Parse(JsonSerializer.Serialize(data)),
            Status = status,
            CreatedAt = updatedAt,
            UpdatedAt = updatedAt
        };

    private static string SearchPath(Guid workspaceId, string query, int limit) =>
        $"/api/workspaces/{workspaceId}/search?query={Uri.EscapeDataString(query)}&limit={limit}";

    private sealed record SeededSearchData(Guid ExactEntryId, Guid LongEntryId);
}
