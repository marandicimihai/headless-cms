using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FastEndpoints.Security;
using FastEndpoints.Testing;
using HeadlessCms.Api.Endpoints.Auth;
using HeadlessCms.Api.Endpoints.Workspaces;
using HeadlessCms.Api.Workspaces.Models;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace HeadlessCms.Api.Tests;

[Collection<TestAppCollection>]
public sealed class WorkspaceEndpointTests(TestApp app) : TestBase
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    protected override async ValueTask SetupAsync() => await app.ResetDatabaseAsync();

    [Fact]
    public async Task WorkspaceCreator_BecomesOwnerAndCanInviteEditor()
    {
        var ct = TestContext.Current.CancellationToken;
        await app.SeedUserAsync("owner@example.test", "owner-password");
        var ownerToken = await LoginAsync("owner@example.test", "owner-password");

        using var createWorkspace = await SendAsync(
            HttpMethod.Post,
            "/api/workspaces",
            ownerToken,
            new { name = "Workspace A" });
        createWorkspace.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await createWorkspace.Content.ReadFromJsonAsync<CreateWorkspaceResponse>(
            JsonOptions,
            cancellationToken: ct);
        created.ShouldNotBeNull();
        created.CurrentRole.ShouldBe(WorkspaceRole.Owner);

        var sender = app.Services.GetRequiredService<TestInvitationEmailSender>();
        sender.Sent.ShouldBeEmpty();

        using var inviteEditor = await SendAsync(
            HttpMethod.Post,
            $"/api/workspaces/{created.Id}/invitations",
            ownerToken,
            new { email = "editor@example.test", role = "Editor" });
        inviteEditor.StatusCode.ShouldBe(HttpStatusCode.Created);
        var editorMessage = sender.Sent.Last();

        await app.SeedUserAsync("editor@example.test", "editor-password");
        var editorToken = await LoginAsync("editor@example.test", "editor-password");
        using var acceptEditor = await SendAsync(
            HttpMethod.Post,
            "/api/auth/invitations/accept",
            editorToken,
            new { token = editorMessage.Token });
        acceptEditor.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var listMembers = await SendAsync(
            HttpMethod.Get,
            $"/api/workspaces/{created.Id}/members",
            ownerToken);
        listMembers.StatusCode.ShouldBe(HttpStatusCode.OK);
        var members = await listMembers.Content
            .ReadFromJsonAsync<ListWorkspaceMembersResponse>(
                JsonOptions,
                cancellationToken: ct);
        members.ShouldNotBeNull();
        members.Total.ShouldBe(2);

        using var reuse = await SendAsync(
            HttpMethod.Post,
            "/api/auth/invitations/accept",
            editorToken,
            new { token = editorMessage.Token });
        reuse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Owner_CanChangeMembersTransferOwnershipAndMustTransferBeforeLeaving()
    {
        var setup = await CreateWorkspaceWithOwnerAndEditorAsync();
        var ct = TestContext.Current.CancellationToken;

        using var ownerLeave = await SendAsync(
            HttpMethod.Delete,
            $"/api/me/workspaces/{setup.WorkspaceId}",
            setup.OwnerToken);
        ownerLeave.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        using var roleChange = await SendAsync(
            HttpMethod.Patch,
            $"/api/workspaces/{setup.WorkspaceId}/members/{setup.EditorId}",
            setup.OwnerToken,
            new { role = "Member" });
        roleChange.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            await roleChange.Content.ReadAsStringAsync(ct));

        using var transfer = await SendAsync(
            HttpMethod.Post,
            $"/api/workspaces/{setup.WorkspaceId}/ownership-transfer",
            setup.OwnerToken,
            new { newOwnerUserId = setup.EditorId });
        transfer.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var formerOwnerLeave = await SendAsync(
            HttpMethod.Delete,
            $"/api/me/workspaces/{setup.WorkspaceId}",
            setup.OwnerToken);
        formerOwnerLeave.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var workspaces = await SendAsync(
            HttpMethod.Get,
            "/api/me/workspaces",
            setup.EditorToken);
        var memberships = await workspaces.Content
            .ReadFromJsonAsync<List<ListMyWorkspacesItemResponse>>(
                JsonOptions,
                cancellationToken: ct);
        memberships.ShouldNotBeNull();
        memberships.Single().CurrentRole.ShouldBe(WorkspaceRole.Owner);
    }

    [Fact]
    public async Task NormalUser_CanCreateWorkspaceAsOwner()
    {
        await app.SeedUserAsync("user@example.test", "user-password");
        var token = await LoginAsync("user@example.test", "user-password");

        using var response = await SendAsync(
            HttpMethod.Post,
            "/api/workspaces",
            token,
            new { name = "Personal workspace" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<CreateWorkspaceResponse>(
            JsonOptions,
            TestContext.Current.CancellationToken);
        created.ShouldNotBeNull();
        created.CurrentRole.ShouldBe(WorkspaceRole.Owner);
    }

    [Fact]
    public async Task Owner_CanListResendAndRevokePendingInvitation()
    {
        var setup = await CreateWorkspaceWithOwnerAndEditorAsync();
        var ct = TestContext.Current.CancellationToken;
        var sender = app.Services.GetRequiredService<TestInvitationEmailSender>();

        using var create = await SendAsync(
            HttpMethod.Post,
            $"/api/workspaces/{setup.WorkspaceId}/invitations",
            setup.OwnerToken,
            new { email = "member@example.test", role = "Member" });
        create.StatusCode.ShouldBe(HttpStatusCode.Created);
        var invitation = await create.Content.ReadFromJsonAsync<CreateWorkspaceInvitationResponse>(
            JsonOptions,
            cancellationToken: ct);
        invitation.ShouldNotBeNull();
        var oldToken = sender.Sent.Last().Token;

        using var resend = await SendAsync(
            HttpMethod.Post,
            $"/api/workspaces/{setup.WorkspaceId}/invitations/{invitation.Id}/resend",
            setup.OwnerToken);
        resend.StatusCode.ShouldBe(HttpStatusCode.OK);
        var newToken = sender.Sent.Last().Token;
        newToken.ShouldNotBe(oldToken);

        using var oldPreview = await app.HttpsClient.PostAsJsonAsync(
            "/api/auth/invitations/preview",
            new { token = oldToken },
            ct);
        oldPreview.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        using var list = await SendAsync(
            HttpMethod.Get,
            $"/api/workspaces/{setup.WorkspaceId}/invitations",
            setup.OwnerToken);
        list.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var editorList = await SendAsync(
            HttpMethod.Get,
            $"/api/workspaces/{setup.WorkspaceId}/invitations",
            setup.EditorToken);
        editorList.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        using var revoke = await SendAsync(
            HttpMethod.Delete,
            $"/api/workspaces/{setup.WorkspaceId}/invitations/{invitation.Id}",
            setup.OwnerToken);
        revoke.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var revokedPreview = await app.HttpsClient.PostAsJsonAsync(
            "/api/auth/invitations/preview",
            new { token = newToken },
            ct);
        revokedPreview.StatusCode.ShouldBe(HttpStatusCode.Gone);
    }

    private async Task<(Guid WorkspaceId, string OwnerToken, string EditorToken, string EditorId)>
        CreateWorkspaceWithOwnerAndEditorAsync()
    {
        var owner = await app.SeedUserAsync("owner@example.test", "owner-password");
        var workspace = await app.SeedWorkspaceAsync((owner, WorkspaceRole.Owner));
        var ownerToken = await LoginAsync("owner@example.test", "owner-password");

        var editorInvite = await app.WithServiceAsync<
            HeadlessCms.Api.Workspaces.Services.WorkspaceInvitationService,
            HeadlessCms.Api.Workspaces.Services.CreatedWorkspaceInvitation>(
            service => service.CreateInvitationAsync(
                owner.Id,
                workspace.Id,
                "editor@example.test",
                WorkspaceRole.Editor));
        var editor = await app.SeedUserAsync("editor@example.test", "editor-password");
        await app.WithServiceAsync<
            HeadlessCms.Api.Workspaces.Services.WorkspaceInvitationService,
            WorkspaceMembership>(
            service => service.AcceptInvitationAsync(editor.Id, editorInvite.Token));
        var editorToken = await LoginAsync("editor@example.test", "editor-password");

        return (workspace.Id, ownerToken, editorToken, editor.Id);
    }

    private async Task<string> LoginAsync(string email, string password)
    {
        using var response = await app.HttpsClient.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return TestApp.ExtractSessionCookie(response);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        string token,
        object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("Cookie", token);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        return await app.HttpsClient.SendAsync(request);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
