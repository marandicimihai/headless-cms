using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FastEndpoints.Security;
using FastEndpoints.Testing;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Endpoints.Auth;
using HeadlessCms.Api.Endpoints.Tenants;
using HeadlessCms.Api.Tenancy.Models;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace HeadlessCms.Api.Tests;

[Collection<TestAppCollection>]
public sealed class TenantEndpointTests(TestApp app) : TestBase
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    protected override async ValueTask SetupAsync() => await app.ResetDatabaseAsync();

    [Fact]
    public async Task InvitationLifecycle_CreatesOwnerAndEditorMemberships()
    {
        var ct = TestContext.Current.CancellationToken;
        await app.SeedUserAsync(
            "admin@example.test",
            "admin-password",
            PlatformRole.PlatformAdmin);
        var adminToken = await LoginAsync("admin@example.test", "admin-password");

        using var createTenant = await SendAsync(
            HttpMethod.Post,
            "/api/tenants",
            adminToken,
            new { name = "Tenant A", ownerEmail = "owner@example.test" });
        createTenant.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await createTenant.Content.ReadFromJsonAsync<CreateTenantResponse>(
            JsonOptions,
            cancellationToken: ct);
        created.ShouldNotBeNull();

        var sender = app.Services.GetRequiredService<TestInvitationEmailSender>();
        var ownerMessage = sender.Sent.Single();
        ownerMessage.Role.ShouldBe(TenantRole.Owner);

        using var previewResponse = await app.HttpsClient.PostAsJsonAsync(
            "/api/auth/invitations/preview",
            new { token = ownerMessage.Token },
            ct);
        previewResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var preview = await previewResponse.Content.ReadFromJsonAsync<InvitationPreviewResponse>(
            JsonOptions,
            cancellationToken: ct);
        preview.ShouldNotBeNull();
        preview.TenantName.ShouldBe("Tenant A");
        preview.MaskedEmail.ShouldNotContain("owner@example.test");

        using var registerResponse = await app.HttpsClient.PostAsJsonAsync(
            "/api/auth/invitations/register",
            new { token = ownerMessage.Token, password = "owner-password" },
            ct);
        registerResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var registration = await registerResponse.Content
            .ReadFromJsonAsync<InvitationRegistrationResponse>(
                JsonOptions,
                cancellationToken: ct);
        registration.ShouldNotBeNull();
        registration.Membership.Role.ShouldBe(TenantRole.Owner);

        using var inviteEditor = await SendAsync(
            HttpMethod.Post,
            $"/api/tenants/{created.Tenant.Id}/invitations",
            registration.Tokens.AccessToken,
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
            $"/api/tenants/{created.Tenant.Id}/members",
            registration.Tokens.AccessToken);
        listMembers.StatusCode.ShouldBe(HttpStatusCode.OK);
        var members = await listMembers.Content
            .ReadFromJsonAsync<PagedResponse<MemberResponse>>(
                JsonOptions,
                cancellationToken: ct);
        members.ShouldNotBeNull();
        members.Total.ShouldBe(2);

        using var adminMemberList = await SendAsync(
            HttpMethod.Get,
            $"/api/tenants/{created.Tenant.Id}/members",
            adminToken);
        adminMemberList.StatusCode.ShouldBe(HttpStatusCode.NotFound);

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
        var setup = await CreateTenantWithOwnerAndEditorAsync();
        var ct = TestContext.Current.CancellationToken;

        using var ownerLeave = await SendAsync(
            HttpMethod.Delete,
            $"/api/me/tenants/{setup.TenantId}",
            setup.OwnerToken);
        ownerLeave.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        using var roleChange = await SendAsync(
            HttpMethod.Patch,
            $"/api/tenants/{setup.TenantId}/members/{setup.EditorId}",
            setup.OwnerToken,
            new { role = "Member" });
        roleChange.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            await roleChange.Content.ReadAsStringAsync(ct));

        using var transfer = await SendAsync(
            HttpMethod.Post,
            $"/api/tenants/{setup.TenantId}/ownership-transfer",
            setup.OwnerToken,
            new { newOwnerUserId = setup.EditorId });
        transfer.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var formerOwnerLeave = await SendAsync(
            HttpMethod.Delete,
            $"/api/me/tenants/{setup.TenantId}",
            setup.OwnerToken);
        formerOwnerLeave.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var tenants = await SendAsync(
            HttpMethod.Get,
            "/api/me/tenants",
            setup.EditorToken);
        var memberships = await tenants.Content
            .ReadFromJsonAsync<List<TenantResponse>>(JsonOptions, cancellationToken: ct);
        memberships.ShouldNotBeNull();
        memberships.Single().CurrentRole.ShouldBe(TenantRole.Owner);
    }

    [Fact]
    public async Task NormalUser_CannotCreateTenant()
    {
        await app.SeedUserAsync("user@example.test", "user-password");
        var token = await LoginAsync("user@example.test", "user-password");

        using var response = await SendAsync(
            HttpMethod.Post,
            "/api/tenants",
            token,
            new { name = "Forbidden", ownerEmail = "owner@example.test" });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Owner_CanListResendAndRevokePendingInvitation()
    {
        var setup = await CreateTenantWithOwnerAndEditorAsync();
        var ct = TestContext.Current.CancellationToken;
        var sender = app.Services.GetRequiredService<TestInvitationEmailSender>();

        using var create = await SendAsync(
            HttpMethod.Post,
            $"/api/tenants/{setup.TenantId}/invitations",
            setup.OwnerToken,
            new { email = "member@example.test", role = "Member" });
        create.StatusCode.ShouldBe(HttpStatusCode.Created);
        var invitation = await create.Content.ReadFromJsonAsync<InvitationResponse>(
            JsonOptions,
            cancellationToken: ct);
        invitation.ShouldNotBeNull();
        var oldToken = sender.Sent.Last().Token;

        using var resend = await SendAsync(
            HttpMethod.Post,
            $"/api/tenants/{setup.TenantId}/invitations/{invitation.Id}/resend",
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
            $"/api/tenants/{setup.TenantId}/invitations",
            setup.OwnerToken);
        list.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var editorList = await SendAsync(
            HttpMethod.Get,
            $"/api/tenants/{setup.TenantId}/invitations",
            setup.EditorToken);
        editorList.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        using var revoke = await SendAsync(
            HttpMethod.Delete,
            $"/api/tenants/{setup.TenantId}/invitations/{invitation.Id}",
            setup.OwnerToken);
        revoke.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var revokedPreview = await app.HttpsClient.PostAsJsonAsync(
            "/api/auth/invitations/preview",
            new { token = newToken },
            ct);
        revokedPreview.StatusCode.ShouldBe(HttpStatusCode.Gone);
    }

    private async Task<(Guid TenantId, string OwnerToken, string EditorToken, string EditorId)>
        CreateTenantWithOwnerAndEditorAsync()
    {
        var admin = await app.SeedUserAsync(
            "admin@example.test",
            "admin-password",
            PlatformRole.PlatformAdmin);
        var created = await app.WithServiceAsync<
            HeadlessCms.Api.Tenancy.Services.TenantInvitationService,
            HeadlessCms.Api.Tenancy.Services.CreatedTenantInvitation>(
            service => service.CreateTenantWithOwnerInvitationAsync(
                admin.Id,
                "Tenant A",
                "owner@example.test"));
        var owner = await app.SeedUserAsync("owner@example.test", "owner-password");
        await app.WithServiceAsync<
            HeadlessCms.Api.Tenancy.Services.TenantInvitationService,
            TenantMembership>(
            service => service.AcceptInvitationAsync(owner.Id, created.Token));
        var ownerToken = await LoginAsync("owner@example.test", "owner-password");

        var editorInvite = await app.WithServiceAsync<
            HeadlessCms.Api.Tenancy.Services.TenantInvitationService,
            HeadlessCms.Api.Tenancy.Services.CreatedTenantInvitation>(
            service => service.CreateInvitationAsync(
                owner.Id,
                created.TenantId,
                "editor@example.test",
                TenantRole.Editor));
        var editor = await app.SeedUserAsync("editor@example.test", "editor-password");
        await app.WithServiceAsync<
            HeadlessCms.Api.Tenancy.Services.TenantInvitationService,
            TenantMembership>(
            service => service.AcceptInvitationAsync(editor.Id, editorInvite.Token));
        var editorToken = await LoginAsync("editor@example.test", "editor-password");

        return (created.TenantId, ownerToken, editorToken, editor.Id);
    }

    private async Task<string> LoginAsync(string email, string password)
    {
        using var response = await app.HttpsClient.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var tokens = await response.Content.ReadFromJsonAsync<TokenResponse>();
        tokens.ShouldNotBeNull();
        return tokens.AccessToken;
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        string token,
        object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
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
