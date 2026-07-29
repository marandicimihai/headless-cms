using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FastEndpoints.Security;
using FastEndpoints.Testing;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Auth.Services;
using HeadlessCms.Api.Endpoints.Auth;
using HeadlessCms.Api.Endpoints.Tenants;
using HeadlessCms.Api.Tenancy.Models;
using HeadlessCms.Api.Tenancy.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace HeadlessCms.Api.Tests;

[Collection<TestAppCollection>]
public sealed class InvitationAndAuthEdgeCaseTests(TestApp app) : TestBase
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    protected override async ValueTask SetupAsync() => await app.ResetDatabaseAsync();

    [Fact]
    public async Task CreateInvitation_RejectsInvalidCallerRoleAndDuplicateTargets()
    {
        var owner = await app.SeedUserAsync("owner", "password");
        var member = await app.SeedUserAsync("member", "password");
        var tenant = await app.SeedTenantAsync(
            (owner, TenantRole.Owner),
            (member, TenantRole.Member));

        var ownerRole = await Should.ThrowAsync<InvitationFlowException>(
            () => CreateInvitationAsync(
                owner.Id,
                tenant.Id,
                "new@example.test",
                TenantRole.Owner));
        ownerRole.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);

        var missingTenant = await Should.ThrowAsync<InvitationFlowException>(
            () => CreateInvitationAsync(
                owner.Id,
                Guid.NewGuid(),
                "new@example.test",
                TenantRole.Editor));
        missingTenant.StatusCode.ShouldBe(StatusCodes.Status404NotFound);

        var nonOwner = await Should.ThrowAsync<InvitationFlowException>(
            () => CreateInvitationAsync(
                member.Id,
                tenant.Id,
                "new@example.test",
                TenantRole.Editor));
        nonOwner.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);

        var existingMember = await Should.ThrowAsync<InvitationFlowException>(
            () => CreateInvitationAsync(
                owner.Id,
                tenant.Id,
                member.Email,
                TenantRole.Editor));
        existingMember.StatusCode.ShouldBe(StatusCodes.Status409Conflict);

        await CreateInvitationAsync(
            owner.Id,
            tenant.Id,
            "pending@example.test",
            TenantRole.Member);
        var duplicate = await Should.ThrowAsync<InvitationFlowException>(
            () => CreateInvitationAsync(
                owner.Id,
                tenant.Id,
                "PENDING@example.test",
                TenantRole.Editor));
        duplicate.StatusCode.ShouldBe(StatusCodes.Status409Conflict);

        var ownerToken = await app.LoginAsync(owner.Email, "password");
        using var duplicateEndpoint = await app.SendAsync(
            HttpMethod.Post,
            $"/api/tenants/{tenant.Id}/invitations",
            ownerToken,
            new { email = "pending@example.test", role = "Member" });
        duplicateEndpoint.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AcceptInvitation_RejectsMissingWrongAndExistingUsers()
    {
        var owner = await app.SeedUserAsync("owner", "password");
        var tenant = await app.SeedTenantAsync((owner, TenantRole.Owner));
        var invitation = await CreateInvitationAsync(
            owner.Id,
            tenant.Id,
            "invited@example.test",
            TenantRole.Editor);
        var wrongUser = await app.SeedUserAsync("wrong", "password");

        var wrongEmail = await Should.ThrowAsync<InvitationFlowException>(
            () => AcceptInvitationAsync(wrongUser.Id, invitation.Token));
        wrongEmail.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);

        var missingUser = await Should.ThrowAsync<InvitationFlowException>(
            () => AcceptInvitationAsync($"missing-{Guid.NewGuid():N}", invitation.Token));
        missingUser.StatusCode.ShouldBe(StatusCodes.Status404NotFound);

        var invitedUser = await app.SeedUserAsync("invited@example.test", "password");
        await app.WithDatabaseAsync(
            async db =>
            {
                db.TenantMemberships.Add(
                    new TenantMembership
                    {
                        TenantId = tenant.Id,
                        UserId = invitedUser.Id,
                        Role = TenantRole.Member
                    });
                await db.SaveChangesAsync();
                return true;
            });

        var existingMembership = await Should.ThrowAsync<InvitationFlowException>(
            () => AcceptInvitationAsync(invitedUser.Id, invitation.Token));
        existingMembership.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task RegistrationAndPreview_RejectExistingExpiredAndAcceptedInvitations()
    {
        var owner = await app.SeedUserAsync("owner", "password");
        var tenant = await app.SeedTenantAsync((owner, TenantRole.Owner));

        var existingAccountInvitation = await CreateInvitationAsync(
            owner.Id,
            tenant.Id,
            "existing@example.test",
            TenantRole.Member);
        await app.SeedUserAsync("existing@example.test", "password");
        var existingAccount = await Should.ThrowAsync<InvitationFlowException>(
            () => RegisterAsync(existingAccountInvitation.Token, "new-password"));
        existingAccount.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
        using var existingAccountEndpoint = await app.HttpsClient.PostAsJsonAsync(
            "/api/auth/invitations/register",
            new
            {
                token = existingAccountInvitation.Token,
                password = "new-password"
            },
            TestContext.Current.CancellationToken);
        existingAccountEndpoint.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var expiredInvitation = await CreateInvitationAsync(
            owner.Id,
            tenant.Id,
            "expired@example.test",
            TenantRole.Member);
        await SetInvitationExpiryAsync(expiredInvitation.InvitationId, DateTime.UtcNow.AddMinutes(-1));

        var expiredPreview = await Should.ThrowAsync<InvitationFlowException>(
            () => PreviewAsync(expiredInvitation.Token));
        expiredPreview.StatusCode.ShouldBe(StatusCodes.Status410Gone);
        var expiredRegistration = await Should.ThrowAsync<InvitationFlowException>(
            () => RegisterAsync(expiredInvitation.Token, "new-password"));
        expiredRegistration.StatusCode.ShouldBe(StatusCodes.Status410Gone);

        var acceptedInvitation = await CreateInvitationAsync(
            owner.Id,
            tenant.Id,
            "accepted@example.test",
            TenantRole.Editor);
        var acceptedUser = await app.SeedUserAsync("accepted@example.test", "password");
        await AcceptInvitationAsync(acceptedUser.Id, acceptedInvitation.Token);

        var acceptedPreview = await Should.ThrowAsync<InvitationFlowException>(
            () => PreviewAsync(acceptedInvitation.Token));
        acceptedPreview.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
        var acceptedRegistration = await Should.ThrowAsync<InvitationFlowException>(
            () => RegisterAsync(acceptedInvitation.Token, "new-password"));
        acceptedRegistration.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task InvitationManagement_HandlesExpiredRevokedAcceptedAndCrossTenantTargets()
    {
        var owner = await app.SeedUserAsync("owner", "password");
        var otherOwner = await app.SeedUserAsync("other-owner", "password");
        var tenant = await app.SeedTenantAsync((owner, TenantRole.Owner));
        var otherTenant = await app.SeedTenantAsync((otherOwner, TenantRole.Owner));
        var ownerToken = await app.LoginAsync(owner.Email, "password");
        var otherOwnerToken = await app.LoginAsync(otherOwner.Email, "password");

        var expired = await CreateInvitationAsync(
            owner.Id,
            tenant.Id,
            "expired@example.test",
            TenantRole.Member);
        await SetInvitationExpiryAsync(expired.InvitationId, DateTime.UtcNow.AddMinutes(-1));
        using var resendExpired = await app.SendAsync(
            HttpMethod.Post,
            InvitationPath(tenant.Id, expired.InvitationId, "resend"),
            ownerToken);
        resendExpired.StatusCode.ShouldBe(HttpStatusCode.OK);

        var revoked = await CreateInvitationAsync(
            owner.Id,
            tenant.Id,
            "revoked@example.test",
            TenantRole.Member);
        using var revoke = await app.SendAsync(
            HttpMethod.Delete,
            InvitationPath(tenant.Id, revoked.InvitationId),
            ownerToken);
        revoke.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var resendRevoked = await app.SendAsync(
            HttpMethod.Post,
            InvitationPath(tenant.Id, revoked.InvitationId, "resend"),
            ownerToken);
        resendRevoked.StatusCode.ShouldBe(HttpStatusCode.Gone);

        var accepted = await CreateInvitationAsync(
            owner.Id,
            tenant.Id,
            "accepted@example.test",
            TenantRole.Editor);
        var acceptedUser = await app.SeedUserAsync("accepted@example.test", "password");
        await AcceptInvitationAsync(acceptedUser.Id, accepted.Token);
        using var resendAccepted = await app.SendAsync(
            HttpMethod.Post,
            InvitationPath(tenant.Id, accepted.InvitationId, "resend"),
            ownerToken);
        resendAccepted.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var revokeAccepted = await app.SendAsync(
            HttpMethod.Delete,
            InvitationPath(tenant.Id, accepted.InvitationId),
            ownerToken);
        revokeAccepted.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        using var crossTenant = await app.SendAsync(
            HttpMethod.Post,
            InvitationPath(tenant.Id, expired.InvitationId, "resend"),
            otherOwnerToken);
        crossTenant.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var mismatchedRoute = await app.SendAsync(
            HttpMethod.Delete,
            InvitationPath(otherTenant.Id, expired.InvitationId),
            otherOwnerToken);
        mismatchedRoute.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PlatformAdmin_CanManageOwnerInvitationOnlyUntilTenantHasOwner()
    {
        var admin = await app.SeedUserAsync(
            "admin",
            "password",
            PlatformRole.PlatformAdmin);
        var adminToken = await app.LoginAsync(admin.Email, "password");
        var created = await app.WithServiceAsync<
            TenantInvitationService,
            CreatedTenantInvitation>(
            service => service.CreateTenantWithOwnerInvitationAsync(
                admin.Id,
                "Tenant A",
                "owner@example.test"));

        using var listBeforeOwner = await app.SendAsync(
            HttpMethod.Get,
            $"/api/tenants/{created.TenantId}/invitations",
            adminToken);
        listBeforeOwner.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await listBeforeOwner.Content.ReadFromJsonAsync<
            PagedResponse<InvitationResponse>>(
            JsonOptions,
            TestContext.Current.CancellationToken);
        page.ShouldNotBeNull();
        page.Items.Single().Role.ShouldBe(TenantRole.Owner);

        using var resend = await app.SendAsync(
            HttpMethod.Post,
            InvitationPath(created.TenantId, created.InvitationId, "resend"),
            adminToken);
        resend.StatusCode.ShouldBe(HttpStatusCode.OK);
        var replacementToken = app.Services
            .GetRequiredService<TestInvitationEmailSender>()
            .Sent
            .Last()
            .Token;

        var owner = await app.SeedUserAsync("owner@example.test", "password");
        await AcceptInvitationAsync(owner.Id, replacementToken);

        using var listAfterOwner = await app.SendAsync(
            HttpMethod.Get,
            $"/api/tenants/{created.TenantId}/invitations",
            adminToken);
        listAfterOwner.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var manageAfterOwner = await app.SendAsync(
            HttpMethod.Post,
            InvitationPath(created.TenantId, created.InvitationId, "resend"),
            adminToken);
        manageAfterOwner.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task InvitationList_FiltersStatusesAndNormalizesPagination()
    {
        var owner = await app.SeedUserAsync("owner", "password");
        var tenant = await app.SeedTenantAsync((owner, TenantRole.Owner));
        var token = await app.LoginAsync(owner.Email, "password");
        await CreateInvitationAsync(
            owner.Id,
            tenant.Id,
            "pending@example.test",
            TenantRole.Member);
        var expired = await CreateInvitationAsync(
            owner.Id,
            tenant.Id,
            "expired@example.test",
            TenantRole.Member);
        await SetInvitationExpiryAsync(expired.InvitationId, DateTime.UtcNow.AddMinutes(-1));

        using var response = await app.SendAsync(
            HttpMethod.Get,
            $"/api/tenants/{tenant.Id}/invitations?status=Expired&page=0&pageSize=500",
            token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<
            PagedResponse<InvitationResponse>>(
            JsonOptions,
            TestContext.Current.CancellationToken);
        page.ShouldNotBeNull();
        page.Page.ShouldBe(1);
        page.PageSize.ShouldBe(100);
        page.Total.ShouldBe(1);
        page.Items.Single().Status.ShouldBe(InvitationStatus.Expired);
    }

    [Fact]
    public async Task RevokedInvitation_CannotBeAcceptedOrRegisteredAndRevokeIsIdempotent()
    {
        var owner = await app.SeedUserAsync("owner", "password");
        var invitee = await app.SeedUserAsync("revoked@example.test", "password");
        var tenant = await app.SeedTenantAsync((owner, TenantRole.Owner));
        var invitation = await CreateInvitationAsync(
            owner.Id,
            tenant.Id,
            invitee.Email,
            TenantRole.Member);
        var ownerToken = await app.LoginAsync(owner.Email, "password");
        var inviteeToken = await app.LoginAsync(invitee.Email, "password");

        using var firstRevoke = await app.SendAsync(
            HttpMethod.Delete,
            InvitationPath(tenant.Id, invitation.InvitationId),
            ownerToken);
        firstRevoke.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var repeatedRevoke = await app.SendAsync(
            HttpMethod.Delete,
            InvitationPath(tenant.Id, invitation.InvitationId),
            ownerToken);
        repeatedRevoke.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var accept = await app.SendAsync(
            HttpMethod.Post,
            "/api/auth/invitations/accept",
            inviteeToken,
            new { token = invitation.Token });
        accept.StatusCode.ShouldBe(HttpStatusCode.Gone);
        using var register = await app.HttpsClient.PostAsJsonAsync(
            "/api/auth/invitations/register",
            new { token = invitation.Token, password = "new-password" },
            TestContext.Current.CancellationToken);
        register.StatusCode.ShouldBe(HttpStatusCode.Gone);
    }

    [Fact]
    public async Task InvalidEmail_NormalizationAndCredentialLookupFailSafely()
    {
        Should.Throw<ArgumentException>(() => EmailNormalizer.Normalize("not-an-email"));

        var result = await app.WithServiceAsync<UserManager, (bool, User?)>(
            service => service.CredentialsAreValidWithUser(
                "not-an-email",
                "password",
                TestContext.Current.CancellationToken));

        result.Item1.ShouldBeFalse();
        result.Item2.ShouldBeNull();

        var blankInvitation = await Should.ThrowAsync<InvitationFlowException>(
            () => PreviewAsync(" "));
        blankInvitation.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task RefreshTokenPersistence_SurfacesPostgreSqlWriteFailures()
    {
        var user = await app.SeedUserAsync("refresh-user", "password");
        using var login = await app.HttpsClient.PostAsJsonAsync(
            "/api/auth/login",
            new { email = user.Email, password = "password" },
            TestContext.Current.CancellationToken);
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        var invalidResponse = await login.Content.ReadFromJsonAsync<TokenResponse>(
            cancellationToken: TestContext.Current.CancellationToken);
        invalidResponse.ShouldNotBeNull();
        var originalTokenHash = TokenHasher.Hash(invalidResponse.RefreshToken);
        invalidResponse.UserId = new string('u', 65);
        invalidResponse.RefreshToken = "replacement-refresh-token";

        await app.WithServiceAsync<Refresh, bool>(
            async service =>
            {
                await Should.ThrowAsync<DbUpdateException>(
                    () => service.PersistTokenAsync(invalidResponse));
                return true;
            });

        var tokenHashes = await app.WithDatabaseAsync(
            db => db.Tokens.Select(token => token.TokenHash).ToListAsync());
        tokenHashes.ShouldBe([originalTokenHash]);
    }

    [Fact]
    public async Task InvitationEndpoints_ValidateBodiesAuthenticationAndShortEmailMasking()
    {
        using var emptyPreview = await app.HttpsClient.PostAsJsonAsync(
            "/api/auth/invitations/preview",
            new { token = string.Empty },
            TestContext.Current.CancellationToken);
        emptyPreview.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var overlongPreview = await app.HttpsClient.PostAsJsonAsync(
            "/api/auth/invitations/preview",
            new { token = new string('t', 513) },
            TestContext.Current.CancellationToken);
        overlongPreview.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        using var emptyRegistrationPassword = await app.HttpsClient.PostAsJsonAsync(
            "/api/auth/invitations/register",
            new { token = "token", password = string.Empty },
            TestContext.Current.CancellationToken);
        emptyRegistrationPassword.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var overlongRegistrationPassword =
            await app.HttpsClient.PostAsJsonAsync(
                "/api/auth/invitations/register",
                new { token = "token", password = new string('p', 65) },
                TestContext.Current.CancellationToken);
        overlongRegistrationPassword.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        using var anonymousAccept = await app.HttpsClient.PostAsJsonAsync(
            "/api/auth/invitations/accept",
            new { token = "token" },
            TestContext.Current.CancellationToken);
        anonymousAccept.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var owner = await app.SeedUserAsync("owner", "password");
        var tenant = await app.SeedTenantAsync((owner, TenantRole.Owner));
        var ownerToken = await app.LoginAsync(owner.Email, "password");
        using var invalidRole = await app.SendAsync(
            HttpMethod.Post,
            $"/api/tenants/{tenant.Id}/invitations",
            ownerToken,
            new { email = "member@example.test", role = "Owner" });
        invalidRole.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var invalidEmail = await app.SendAsync(
            HttpMethod.Post,
            $"/api/tenants/{tenant.Id}/invitations",
            ownerToken,
            new { email = "not-an-email", role = "Member" });
        invalidEmail.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var shortEmailInvitation = await CreateInvitationAsync(
            owner.Id,
            tenant.Id,
            "a@example.test",
            TenantRole.Member);
        using var preview = await app.HttpsClient.PostAsJsonAsync(
            "/api/auth/invitations/preview",
            new { token = shortEmailInvitation.Token },
            TestContext.Current.CancellationToken);
        preview.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await preview.Content.ReadFromJsonAsync<InvitationPreviewResponse>(
            JsonOptions,
            cancellationToken: TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();
        body.MaskedEmail.ShouldBe("*@example.test");
    }

    private Task<CreatedTenantInvitation> CreateInvitationAsync(
        string ownerId,
        Guid tenantId,
        string email,
        TenantRole role) =>
        app.WithServiceAsync<TenantInvitationService, CreatedTenantInvitation>(
            service => service.CreateInvitationAsync(ownerId, tenantId, email, role));

    private Task<TenantMembership> AcceptInvitationAsync(string userId, string token) =>
        app.WithServiceAsync<TenantInvitationService, TenantMembership>(
            service => service.AcceptInvitationAsync(userId, token));

    private Task<InvitationRegistration> RegisterAsync(string token, string password) =>
        app.WithServiceAsync<TenantInvitationService, InvitationRegistration>(
            service => service.RegisterAsync(token, password));

    private Task<InvitationPreview> PreviewAsync(string token) =>
        app.WithServiceAsync<TenantInvitationService, InvitationPreview>(
            service => service.PreviewAsync(token));

    private Task SetInvitationExpiryAsync(Guid invitationId, DateTime expiresAt) =>
        app.WithDatabaseAsync(
            async db =>
            {
                var invitation = await db.TenantInvitations.SingleAsync(
                    candidate => candidate.Id == invitationId);
                invitation.ExpiresAt = expiresAt;
                await db.SaveChangesAsync();
                return true;
            });

    private static string InvitationPath(
        Guid tenantId,
        Guid invitationId,
        string? action = null) =>
        $"/api/tenants/{tenantId}/invitations/{invitationId}" +
        (action is null ? string.Empty : $"/{action}");

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
