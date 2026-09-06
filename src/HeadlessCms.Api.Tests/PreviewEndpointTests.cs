using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FastEndpoints.Testing;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Endpoints.Projects;
using HeadlessCms.Api.Endpoints.Workspaces;
using HeadlessCms.Api.Workspaces.Models;
using Shouldly;
using Xunit;

namespace HeadlessCms.Api.Tests;

[Collection<TestAppCollection>]
public sealed class PreviewEndpointTests(TestApp app) : TestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    protected override async ValueTask SetupAsync() => await app.ResetDatabaseAsync();

    [Fact]
    public async Task Previews_AggregateScopedContentAndLimitRecentEntries()
    {
        var owner = await app.SeedUserAsync("owner", "password");
        var member = await app.SeedUserAsync("member", "password");
        var workspace = await app.SeedWorkspaceAsync((owner, WorkspaceRole.Owner), (member, WorkspaceRole.Member));
        var otherWorkspace = await app.SeedWorkspaceAsync((owner, WorkspaceRole.Owner));
        var project = await app.SeedProjectAsync(workspace.Id, "Alpha");
        await app.SeedProjectAsync(workspace.Id, "Empty");
        var otherProject = await app.SeedProjectAsync(otherWorkspace.Id, "Other");
        var now = DateTime.UtcNow;
        await app.WithDatabaseAsync(async db =>
        {
            foreach (var p in new[] { project, otherProject })
            {
                var type = new ContentType { WorkspaceId = p.WorkspaceId, ProjectId = p.Id, Key = "article", CreatedAt = now, UpdatedAt = now };
                db.ContentTypes.Add(type);
                for (var i = 0; i < 12; i++)
                    db.ContentEntries.Add(new ContentEntry
                    {
                        WorkspaceId = p.WorkspaceId, ProjectId = p.Id, ContentType = type,
                        Data = JsonDocument.Parse("{}"), Status = i % 2 == 0 ? ContentEntryStatus.Published : ContentEntryStatus.Draft,
                        CreatedAt = now.AddDays(-1), UpdatedAt = now.AddMinutes(i)
                    });
            }
            for (var i = 0; i < 4; i++)
                db.WorkspaceInvitations.Add(new WorkspaceInvitation
                {
                    WorkspaceId = workspace.Id, Email = $"invite{i}@example.test", TokenHash = new string((char)('a' + i), 64),
                    InvitedByUserId = owner.Id, Role = WorkspaceRole.Member,
                    ExpiresAt = i == 1 ? now.AddDays(-1) : now.AddDays(1),
                    AcceptedAt = i == 2 ? now : null, RevokedAt = i == 3 ? now : null
                });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            return true;
        });
        var token = await app.LoginAsync(owner.Email, "password");
        var workspaceResponse = await app.SendAsync(HttpMethod.Get, $"/api/workspaces/{workspace.Id}/preview", token);
        workspaceResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var summary = await workspaceResponse.Content.ReadFromJsonAsync<GetWorkspacePreviewResponse>(JsonOptions, cancellationToken: TestContext.Current.CancellationToken);
        summary.ShouldNotBeNull();
        summary.ProjectCount.ShouldBe(2);
        summary.EntryCount.ShouldBe(12);
        summary.PublishedEntryCount.ShouldBe(6);
        summary.DraftEntryCount.ShouldBe(6);
        summary.MemberCount.ShouldBe(2);
        summary.PendingInvitationCount.ShouldBe(1);
        summary.Projects.Select(p => p.Name).ShouldBe(["Alpha", "Empty"]);
        summary.Projects[0].ContentTypeCount.ShouldBe(1);
        summary.Projects[0].EntryCount.ShouldBe(12);
        summary.Projects[0].LastContentUpdatedAt.ShouldNotBeNull();
        summary.Projects[1].LastContentUpdatedAt.ShouldBeNull();
        summary.Projects[1].EntryCount.ShouldBe(0);

        var response = await app.SendAsync(HttpMethod.Get, $"{TestApp.ProjectPath(workspace.Id, project.Id)}/preview", token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var preview = await response.Content.ReadFromJsonAsync<GetProjectPreviewResponse>(JsonOptions, cancellationToken: TestContext.Current.CancellationToken);
        preview.ShouldNotBeNull();
        preview.ContentTypeCount.ShouldBe(1);
        preview.PublishedEntryCount.ShouldBe(6);
        preview.DraftEntryCount.ShouldBe(6);
        preview.RecentEntries.Count.ShouldBe(10);
        preview.RecentEntries.ShouldAllBe(e => e.ContentTypeKey == "article");
        preview.RecentEntries.Select(e => e.UpdatedAt).ShouldBe(preview.RecentEntries.Select(e => e.UpdatedAt).OrderDescending());
        preview.RecentEntries[0].UpdatedAt.ShouldBe(now.AddMinutes(11), TimeSpan.FromMilliseconds(1));

        var memberToken = await app.LoginAsync(member.Email, "password");
        var memberResponse = await app.SendAsync(HttpMethod.Get, $"/api/workspaces/{workspace.Id}/preview", memberToken);
        memberResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var memberSummary = await memberResponse.Content.ReadFromJsonAsync<GetWorkspacePreviewResponse>(JsonOptions, cancellationToken: TestContext.Current.CancellationToken);
        memberSummary.ShouldNotBeNull();
        memberSummary.PendingInvitationCount.ShouldBeNull();
        memberSummary.CurrentRole.ShouldBe(WorkspaceRole.Member);
    }

    [Fact]
    public async Task Previews_EnforceMembershipAndProjectBoundaryAndReturnEmptyCounts()
    {
        var member = await app.SeedUserAsync("member", "password");
        var admin = await app.SeedUserAsync("admin", "password", PlatformRole.PlatformAdmin);
        var workspace = await app.SeedWorkspaceAsync((member, WorkspaceRole.Member));
        var other = await app.SeedWorkspaceAsync((member, WorkspaceRole.Member));
        var project = await app.SeedProjectAsync(workspace.Id, "Empty project");
        var token = await app.LoginAsync(member.Email, "password");
        var adminToken = await app.LoginAsync(admin.Email, "password");
        foreach (var path in new[] { $"/api/workspaces/{workspace.Id}/preview", $"{TestApp.ProjectPath(workspace.Id, project.Id)}/preview" })
        {
            (await app.SendAsync(HttpMethod.Get, path, adminToken)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await app.HttpsClient.GetAsync(path, TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }
        (await app.SendAsync(HttpMethod.Get, $"{TestApp.ProjectPath(other.Id, project.Id)}/preview", token)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await app.SendAsync(HttpMethod.Get, $"/api/workspaces/{Guid.NewGuid()}/preview", token)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var response = await app.SendAsync(HttpMethod.Get, $"{TestApp.ProjectPath(workspace.Id, project.Id)}/preview", token);
        var preview = await response.Content.ReadFromJsonAsync<GetProjectPreviewResponse>(JsonOptions, cancellationToken: TestContext.Current.CancellationToken);
        preview.ShouldNotBeNull();
        preview.ContentTypeCount.ShouldBe(0);
        preview.PublishedEntryCount.ShouldBe(0);
        preview.DraftEntryCount.ShouldBe(0);
        preview.RecentEntries.ShouldBeEmpty();
        var emptyResponse = await app.SendAsync(HttpMethod.Get, $"/api/workspaces/{other.Id}/preview", token);
        var empty = await emptyResponse.Content.ReadFromJsonAsync<GetWorkspacePreviewResponse>(JsonOptions, cancellationToken: TestContext.Current.CancellationToken);
        empty.ShouldNotBeNull();
        empty.ProjectCount.ShouldBe(0);
        empty.EntryCount.ShouldBe(0);
        empty.MemberCount.ShouldBe(1);
        empty.Projects.ShouldBeEmpty();
    }
}
