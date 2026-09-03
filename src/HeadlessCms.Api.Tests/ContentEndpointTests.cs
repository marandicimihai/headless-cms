using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FastEndpoints;
using FastEndpoints.Security;
using FastEndpoints.Testing;
using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Endpoints.Auth;
using HeadlessCms.Api.Endpoints.Content;
using HeadlessCms.Api.Endpoints.Projects;
using HeadlessCms.Api.Workspaces.Models;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace HeadlessCms.Api.Tests;

[Collection<TestAppCollection>]
public sealed class ContentEndpointTests(TestApp app) : TestBase
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

    [Fact]
    public async Task Owner_CanDefineValidateAndQueryDynamicContent()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await app.SeedUserAsync("owner", "owner-password");
        var workspace = await app.SeedWorkspaceAsync((owner, WorkspaceRole.Owner));
        var token = await LoginAsync("owner", "owner-password");
        var project = await CreateProjectAsync(workspace.Id, token);

        var createType = await SendAsync(
            HttpMethod.Post,
            ContentTypesPath(workspace.Id, project.Id),
            token,
            ArticleDefinition());

        createType.StatusCode.ShouldBe(HttpStatusCode.Created);
        var createTypeBody = await createType.Content.ReadAsStringAsync(ct);
        using var createTypeJson = JsonDocument.Parse(createTypeBody);
        createTypeJson.RootElement.TryGetProperty("name", out _).ShouldBeFalse();
        createTypeJson.RootElement.GetProperty("fields")[0]
            .TryGetProperty("name", out _).ShouldBeFalse();
        var type = JsonSerializer.Deserialize<CreateContentTypeResponse>(
            createTypeBody,
            JsonOptions);
        type.ShouldNotBeNull();
        type.Key.ShouldBe("article");
        type.ProjectId.ShouldBe(project.Id);
        type.Fields.Count.ShouldBe(3);

        var first = await CreateEntryAsync(
            workspace.Id,
            project.Id,
            token,
            new { title = "First", views = 10, published = false });
        var second = await CreateEntryAsync(
            workspace.Id,
            project.Id,
            token,
            new { title = "Second", views = 125, published = true });

        second.Data.GetProperty("views").GetInt32().ShouldBe(125);

        var invalid = await SendAsync(
            HttpMethod.Post,
            EntriesPath(workspace.Id, project.Id),
            token,
            new
            {
                data = new { title = "Invalid", views = "many", unexpected = true }
            });
        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var list = await SendAsync(
            HttpMethod.Get,
            EntriesPath(workspace.Id, project.Id) +
            "?filter[views][gte]=100&sort=-views",
            token);
        list.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await list.Content.ReadFromJsonAsync<ListContentEntriesResponse>(
            JsonOptions,
            cancellationToken: ct);
        page.ShouldNotBeNull();
        page.Total.ShouldBe(1);
        page.Items.Single().Id.ShouldBe(second.Id);

        var stored = await app.WithDatabaseAsync(
            db => db.ContentEntries.AsNoTracking().ToListAsync(ct));
        stored.Count.ShouldBe(2);
        stored.ShouldAllBe(entry => entry.WorkspaceId == workspace.Id);
        stored.ShouldAllBe(entry => entry.ProjectId == project.Id);
    }

    [Theory]
    [InlineData(WorkspaceRole.Owner, HttpStatusCode.Created)]
    [InlineData(WorkspaceRole.Editor, HttpStatusCode.Created)]
    [InlineData(WorkspaceRole.Member, HttpStatusCode.Forbidden)]
    public async Task ContentWrites_UseWorkspaceMembershipRole(
        WorkspaceRole role,
        HttpStatusCode expected)
    {
        var user = await app.SeedUserAsync(role.ToString().ToLowerInvariant(), "password");
        var workspace = await app.SeedWorkspaceAsync((user, role));
        var project = await SeedProjectAsync(workspace.Id);
        var token = await LoginAsync(role.ToString().ToLowerInvariant(), "password");

        var response = await SendAsync(
            HttpMethod.Post,
            ContentTypesPath(workspace.Id, project.Id),
            token,
            ArticleDefinition());

        response.StatusCode.ShouldBe(expected);
    }

    [Fact]
    public async Task ContentTypeDefinition_RemovesUnsupportedFieldSettings()
    {
        var owner = await app.SeedUserAsync("owner", "owner-password");
        var workspace = await app.SeedWorkspaceAsync((owner, WorkspaceRole.Owner));
        var token = await LoginAsync("owner", "owner-password");
        var project = await CreateProjectAsync(workspace.Id, token);

        var response = await SendAsync(
            HttpMethod.Post,
            ContentTypesPath(workspace.Id, project.Id),
            token,
            new
            {
                key = "article",
                fields = new[]
                {
                    new
                    {
                        key = "title",
                        type = "text",
                        settings = new { @default = "Untitled", unexpected = true }
                    }
                }
            });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var responseJson = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var settings = responseJson.RootElement
            .GetProperty("fields")[0]
            .GetProperty("settings");
        settings.GetProperty("default").GetString().ShouldBe("Untitled");
        settings.TryGetProperty("unexpected", out _).ShouldBeFalse();

        var persistedSettings = await app.WithDatabaseAsync(async db =>
            (await db.ContentFields.SingleAsync()).Settings);
        persistedSettings.GetProperty("default").GetString().ShouldBe("Untitled");
        persistedSettings.TryGetProperty("unexpected", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task OptionalFields_ReplaceNullAndMissingValuesButPreserveEmptyTextOnCreateAndUpdate()
    {
        var owner = await app.SeedUserAsync("owner", "owner-password");
        var workspace = await app.SeedWorkspaceAsync((owner, WorkspaceRole.Owner));
        var token = await LoginAsync("owner", "owner-password");
        var project = await CreateProjectAsync(workspace.Id, token);

        var definition = await SendAsync(
            HttpMethod.Post,
            ContentTypesPath(workspace.Id, project.Id),
            token,
            new
            {
                key = "article",
                fields = new object[]
                {
                    new { key = "title", type = "text", required = true },
                    new
                    {
                        key = "summary",
                        type = "text",
                        settings = new { @default = "Untitled" }
                    },
                    new { key = "published", type = "boolean", required = true }
                }
            });
        definition.StatusCode.ShouldBe(HttpStatusCode.Created);

        var nullValue = await CreateEntryAsync(
            workspace.Id,
            project.Id,
            token,
            new { title = "Null value", summary = (string?)null, published = true });
        nullValue.Data.GetProperty("summary").GetString().ShouldBe("Untitled");

        var emptyValue = await CreateEntryAsync(
            workspace.Id,
            project.Id,
            token,
            new { title = "Empty value", summary = "", published = true });
        emptyValue.Data.GetProperty("summary").GetString().ShouldBe(string.Empty);

        var whitespaceValue = await CreateEntryAsync(
            workspace.Id,
            project.Id,
            token,
            new { title = "Whitespace value", summary = "   ", published = true });
        whitespaceValue.Data.GetProperty("summary").GetString().ShouldBe("   ");

        var omittedValue = await CreateEntryAsync(
            workspace.Id,
            project.Id,
            token,
            new { title = "Omitted value", published = true });
        omittedValue.Data.GetProperty("summary").GetString().ShouldBe("Untitled");

        var update = await SendAsync(
            HttpMethod.Put,
            $"{EntriesPath(workspace.Id, project.Id)}/{nullValue.Id}",
            token,
            new { data = new { title = "Updated", published = false } });
        update.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await update.Content.ReadFromJsonAsync<UpdateContentEntryResponse>(
            JsonOptions,
            cancellationToken: TestContext.Current.CancellationToken);
        updated.ShouldNotBeNull();
        updated.Data.GetProperty("summary").GetString().ShouldBe("Untitled");
    }

    [Fact]
    public async Task Membership_DoesNotGrantAccessToAnotherWorkspace()
    {
        var user = await app.SeedUserAsync("editor", "password");
        await app.SeedWorkspaceAsync((user, WorkspaceRole.Editor));
        var otherWorkspace = await app.WithDatabaseAsync(
            async db =>
            {
                var workspace = new Workspace { Name = "Workspace B" };
                db.Workspaces.Add(workspace);
                await db.SaveChangesAsync();
                return workspace;
            });
        var otherProject = await SeedProjectAsync(otherWorkspace.Id);
        var token = await LoginAsync("editor", "password");

        var create = await SendAsync(
            HttpMethod.Post,
            ContentTypesPath(otherWorkspace.Id, otherProject.Id),
            token,
            ArticleDefinition());
        create.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var list = await SendAsync(
            HttpMethod.Get,
            ContentTypesPath(otherWorkspace.Id, otherProject.Id),
            token);
        list.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ContentTypesAndEntries_AreIsolatedBetweenProjects()
    {
        var owner = await app.SeedUserAsync("owner", "password");
        var workspace = await app.SeedWorkspaceAsync((owner, WorkspaceRole.Owner));
        var firstProject = await SeedProjectAsync(workspace.Id);
        var secondProject = await SeedProjectAsync(workspace.Id);
        var token = await LoginAsync("owner", "password");

        var firstType = await SendAsync(
            HttpMethod.Post,
            ContentTypesPath(workspace.Id, firstProject.Id),
            token,
            ArticleDefinition());
        var secondType = await SendAsync(
            HttpMethod.Post,
            ContentTypesPath(workspace.Id, secondProject.Id),
            token,
            ArticleDefinition());

        firstType.StatusCode.ShouldBe(HttpStatusCode.Created);
        secondType.StatusCode.ShouldBe(HttpStatusCode.Created);

        var entry = await CreateEntryAsync(
            workspace.Id,
            firstProject.Id,
            token,
            new { title = "First project", views = 1, published = false });

        var crossProjectRead = await SendAsync(
            HttpMethod.Get,
            $"{EntriesPath(workspace.Id, secondProject.Id)}/{entry.Id}",
            token);
        crossProjectRead.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var storedTypes = await app.WithDatabaseAsync(
            db => db.ContentTypes
                .AsNoTracking()
                .Where(type => type.WorkspaceId == workspace.Id)
                .ToListAsync());
        storedTypes.Count.ShouldBe(2);
        storedTypes.Select(type => type.ProjectId)
            .ShouldBe([firstProject.Id, secondProject.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task DefinitionUpdates_MigrateExistingEntriesToTheCurrentSchema()
    {
        var ct = TestContext.Current.CancellationToken;
        var editor = await app.SeedUserAsync("editor", "password");
        var workspace = await app.SeedWorkspaceAsync((editor, WorkspaceRole.Editor));
        var project = await SeedProjectAsync(workspace.Id);
        var token = await LoginAsync("editor", "password");

        await SendAsync(
            HttpMethod.Post,
            ContentTypesPath(workspace.Id, project.Id),
            token,
            ArticleDefinition());
        var oldEntry = await CreateEntryAsync(
            workspace.Id,
            project.Id,
            token,
            new { title = "Old", views = 1, published = false });

        var updateType = await SendAsync(
            HttpMethod.Put,
            ContentTypePath(workspace.Id, project.Id),
            token,
            new
            {
                fields = new object[]
                {
                    new { key = "title", type = "text", required = true },
                    new { key = "views", type = "number" },
                    new { key = "published", type = "boolean", required = true },
                    new
                    {
                        key = "summary",
                        type = "text",
                        required = false,
                        settings = new { @default = "No summary" }
                    }
                }
            });

        updateType.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updatedType = await updateType.Content.ReadFromJsonAsync<UpdateContentTypeResponse>(
            JsonOptions,
            cancellationToken: ct);
        updatedType.ShouldNotBeNull();
        updatedType.Fields.Count.ShouldBe(4);

        var migrated = await SendAsync(
            HttpMethod.Get,
            $"{EntriesPath(workspace.Id, project.Id)}/{oldEntry.Id}",
            token);
        migrated.StatusCode.ShouldBe(HttpStatusCode.OK);
        var migratedEntry = await migrated.Content.ReadFromJsonAsync<GetContentEntryResponse>(
            JsonOptions,
            cancellationToken: ct);
        migratedEntry.ShouldNotBeNull();
        migratedEntry.Data.GetProperty("summary").GetString().ShouldBe("No summary");

        var updateOldEntry = await SendAsync(
            HttpMethod.Put,
            $"{EntriesPath(workspace.Id, project.Id)}/{oldEntry.Id}",
            token,
            new
            {
                data = new
                {
                    title = "Uses current schema",
                    views = 3,
                    published = true,
                    summary = "Migrated"
                }
            });
        updateOldEntry.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await updateOldEntry.Content.ReadFromJsonAsync<UpdateContentEntryResponse>(
            JsonOptions,
            cancellationToken: ct);
        updated.ShouldNotBeNull();
        updated.Data.GetProperty("summary").GetString().ShouldBe("Migrated");

        var incompatible = await SendAsync(
            HttpMethod.Put,
            ContentTypePath(workspace.Id, project.Id),
            token,
            new
            {
                fields = new object[]
                {
                    new { key = "title", type = "number", required = true }
                }
            });
        incompatible.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DefinitionUpdate_RollsBackWhenExistingEntriesCannotSatisfyIt()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await app.SeedUserAsync("owner", "password");
        var workspace = await app.SeedWorkspaceAsync((owner, WorkspaceRole.Owner));
        var project = await SeedProjectAsync(workspace.Id);
        var token = await LoginAsync("owner", "password");

        await SendAsync(
            HttpMethod.Post,
            ContentTypesPath(workspace.Id, project.Id),
            token,
            ArticleDefinition());
        var entry = await CreateEntryAsync(
            workspace.Id,
            project.Id,
            token,
            new { title = "Existing", views = 1, published = false });

        var rejected = await SendAsync(
            HttpMethod.Put,
            ContentTypePath(workspace.Id, project.Id),
            token,
            new
            {
                fields = new object[]
                {
                    new { key = "title", type = "text", required = true },
                    new { key = "views", type = "number" },
                    new { key = "published", type = "boolean", required = true },
                    new { key = "summary", type = "text", required = true }
                }
            });

        rejected.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var definition = await SendAsync(
            HttpMethod.Get,
            ContentTypePath(workspace.Id, project.Id),
            token);
        var current = await definition.Content.ReadFromJsonAsync<GetContentTypeResponse>(
            JsonOptions,
            cancellationToken: ct);
        current.ShouldNotBeNull();
        current.Key.ShouldBe("article");
        current.Fields.Select(field => field.Key).ShouldNotContain("summary");

        var stored = await SendAsync(
            HttpMethod.Get,
            $"{EntriesPath(workspace.Id, project.Id)}/{entry.Id}",
            token);
        var unchanged = await stored.Content.ReadFromJsonAsync<GetContentEntryResponse>(
            JsonOptions,
            cancellationToken: ct);
        unchanged.ShouldNotBeNull();
        unchanged.Data.TryGetProperty("summary", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task DefinitionUpdate_RemovesDeletedFieldsFromEveryEntry()
    {
        var ct = TestContext.Current.CancellationToken;
        var editor = await app.SeedUserAsync("editor", "password");
        var workspace = await app.SeedWorkspaceAsync((editor, WorkspaceRole.Editor));
        var project = await SeedProjectAsync(workspace.Id);
        var token = await LoginAsync("editor", "password");

        await SendAsync(
            HttpMethod.Post,
            ContentTypesPath(workspace.Id, project.Id),
            token,
            ArticleDefinition());
        var entry = await CreateEntryAsync(
            workspace.Id,
            project.Id,
            token,
            new { title = "Existing", views = 1, published = false });

        var update = await SendAsync(
            HttpMethod.Put,
            ContentTypePath(workspace.Id, project.Id),
            token,
            new
            {
                fields = new object[]
                {
                    new { key = "title", type = "text", required = true },
                    new { key = "published", type = "boolean", required = true }
                }
            });

        update.StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await SendAsync(
            HttpMethod.Get,
            $"{EntriesPath(workspace.Id, project.Id)}/{entry.Id}",
            token);
        var migrated = await response.Content.ReadFromJsonAsync<GetContentEntryResponse>(
            JsonOptions,
            cancellationToken: ct);
        migrated.ShouldNotBeNull();
        migrated.Data.TryGetProperty("views", out _).ShouldBeFalse();
        migrated.Data.GetProperty("title").GetString().ShouldBe("Existing");
    }

    [Fact]
    public async Task OwnerCanDeleteContentTypeAndItsEntries()
    {
        var owner = await app.SeedUserAsync("owner", "password");
        var workspace = await app.SeedWorkspaceAsync((owner, WorkspaceRole.Owner));
        var project = await SeedProjectAsync(workspace.Id);
        var token = await LoginAsync("owner", "password");

        await SendAsync(
            HttpMethod.Post,
            ContentTypesPath(workspace.Id, project.Id),
            token,
            ArticleDefinition());
        await CreateEntryAsync(
            workspace.Id,
            project.Id,
            token,
            new { title = "Delete me", views = 1, published = false });

        var deleted = await SendAsync(
            HttpMethod.Delete,
            ContentTypePath(workspace.Id, project.Id),
            token);

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var stored = await app.WithDatabaseAsync(async db => new
        {
            Types = await db.ContentTypes.CountAsync(),
            Fields = await db.ContentFields.CountAsync(),
            Entries = await db.ContentEntries.CountAsync()
        });
        stored.Types.ShouldBe(0);
        stored.Fields.ShouldBe(0);
        stored.Entries.ShouldBe(0);
    }

    private static object ArticleDefinition() =>
        new
        {
            key = "article",
            fields = new object[]
            {
                new
                {
                    key = "title",
                    type = "text",
                    required = true
                },
                new
                {
                    key = "views",
                    type = "number",
                    settings = new { @default = 0 }
                },
                new
                {
                    key = "published",
                    type = "boolean",
                    required = true
                }
            }
        };

    private async Task<CreateContentEntryResponse> CreateEntryAsync(
        Guid workspaceId,
        Guid projectId,
        string token,
        object data)
    {
        var response = await SendAsync(
            HttpMethod.Post,
            EntriesPath(workspaceId, projectId),
            token,
            new { data });
        var responseBody = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, responseBody);
        var entry = await response.Content.ReadFromJsonAsync<CreateContentEntryResponse>(
            JsonOptions,
            cancellationToken: TestContext.Current.CancellationToken);
        entry.ShouldNotBeNull();
        return entry;
    }

    private async Task<CreateProjectResponse> CreateProjectAsync(
        Guid workspaceId,
        string token)
    {
        var response = await SendAsync(
            HttpMethod.Post,
            $"/api/workspaces/{workspaceId}/projects",
            token,
            new { name = "Website" });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var project = await response.Content.ReadFromJsonAsync<CreateProjectResponse>(
            cancellationToken: TestContext.Current.CancellationToken);
        project.ShouldNotBeNull();
        return project;
    }

    private Task<Project> SeedProjectAsync(Guid workspaceId) =>
        app.WithDatabaseAsync(
            async db =>
            {
                var now = DateTime.UtcNow;
                var project = new Project
                {
                    WorkspaceId = workspaceId,
                    Name = $"Project {Guid.NewGuid():N}",
                    CreatedAt = now,
                    UpdatedAt = now
                };
                db.Projects.Add(project);
                await db.SaveChangesAsync();
                return project;
            });

    private static string ContentTypesPath(Guid workspaceId, Guid projectId) =>
        $"/api/workspaces/{workspaceId}/projects/{projectId}/content-types";

    private static string ContentTypePath(Guid workspaceId, Guid projectId) =>
        $"{ContentTypesPath(workspaceId, projectId)}/article";

    private static string EntriesPath(Guid workspaceId, Guid projectId) =>
        $"{ContentTypePath(workspaceId, projectId)}/entries";

    private Task<string> LoginAsync(string identifier, string password) =>
        app.LoginAsync(identifier, password);

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        string accessToken,
        object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("Cookie", accessToken);
        if (body is not null)
            request.Content = JsonContent.Create(body);

        return await app.HttpsClient.SendAsync(
            request,
            TestContext.Current.CancellationToken);
    }
}
