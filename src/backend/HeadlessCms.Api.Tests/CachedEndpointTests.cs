using System.Net;
using FastEndpoints.Testing;
using HeadlessCms.Api.Workspaces.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace HeadlessCms.Api.Tests;

[Collection<TestAppCollection>]
public sealed class CachedEndpointTests(TestApp app) : TestBase
{
    protected override async ValueTask SetupAsync() => await app.ResetDatabaseAsync();

    [Fact]
    public async Task Warm_resources_skip_queries_but_keep_sessions_and_memberships_live()
    {
        var owner = await app.SeedUserAsync("cache-owner", "owner-password");
        var workspace = await app.SeedWorkspaceAsync((owner, WorkspaceRole.Owner));
        var cookie = await app.LoginAsync(owner.Email, "owner-password");
        var path = TestApp.ProjectsPath(workspace.Id);
        (await app.SendAsync(HttpMethod.Post, path, cookie, new { name = "Cached project" })).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await app.SendAsync(HttpMethod.Get, path, cookie)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var counts = app.Services.GetRequiredService<ResourceQueryCounter>();
        var resources = counts.Resources; var memberships = counts.Memberships; var sessions = counts.Sessions;
        (await app.SendAsync(HttpMethod.Get, path, cookie)).StatusCode.ShouldBe(HttpStatusCode.OK);
        counts.Resources.ShouldBe(resources);
        counts.Memberships.ShouldBeGreaterThan(memberships);
        counts.Sessions.ShouldBeGreaterThan(sessions);
        (await app.SendAsync(HttpMethod.Post, path, cookie, new { name = "x" })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await app.SendAsync(HttpMethod.Get, path, cookie)).StatusCode.ShouldBe(HttpStatusCode.OK);
        counts.Resources.ShouldBe(resources);
        (await app.SendAsync(HttpMethod.Post, path, cookie, new { name = "Another project" })).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await app.SendAsync(HttpMethod.Get, path, cookie)).StatusCode.ShouldBe(HttpStatusCode.OK);
        counts.Resources.ShouldBeGreaterThan(resources);
        await app.WithDatabaseAsync(async db =>
        {
            db.WorkspaceMemberships.Remove(await db.WorkspaceMemberships.SingleAsync(m => m.WorkspaceId == workspace.Id));
            return await db.SaveChangesAsync();
        });
        (await app.SendAsync(HttpMethod.Get, path, cookie)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await app.SendAsync(HttpMethod.Post, "/api/auth/logout", cookie);
        (await app.SendAsync(HttpMethod.Get, path, cookie)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
