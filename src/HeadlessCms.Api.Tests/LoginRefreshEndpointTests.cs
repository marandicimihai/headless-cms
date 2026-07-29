using System.Net;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FastEndpoints;
using FastEndpoints.Security;
using FastEndpoints.Testing;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Auth.Services;
using HeadlessCms.Api.Endpoints.Auth;
using HeadlessCms.Api.Tenancy.Models;
using HeadlessCms.Api.Tenancy.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Shouldly;
using Xunit;

namespace HeadlessCms.Api.Tests;

[Collection<TestAppCollection>]
public sealed class LoginRefreshEndpointTests(TestApp app) : TestBase
{
    private const string EmailAddress = "admin@example.test";
    private const string Password = "correct-password";

    protected override async ValueTask SetupAsync()
    {
        await app.ResetDatabaseAsync();
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsAndPersistsTokenPair()
    {
        var user = await app.SeedUserAsync(EmailAddress, Password);
        var beforeLogin = DateTime.UtcNow;

        var (response, tokens) =
            await app.HttpsClient.POSTAsync<Login, LoginRequest, TokenResponse>(
                new LoginRequest
                {
                    Email = EmailAddress.ToUpperInvariant(),
                    Password = Password
                });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        tokens.UserId.ShouldBe(user.Id);
        tokens.AccessToken.ShouldNotBeNullOrWhiteSpace();
        tokens.RefreshToken.ShouldNotBeNullOrWhiteSpace();

        var (principal, jwt) = ValidateAccessToken(tokens.AccessToken);
        principal.FindFirstValue("sub").ShouldBe(user.Id);
        principal.FindFirstValue("email").ShouldBe(user.Email);
        principal.FindFirstValue("role").ShouldBe(nameof(PlatformRole.User));
        jwt.ValidTo.ShouldBeGreaterThan(beforeLogin);
        jwt.ValidTo.ShouldBeLessThan(beforeLogin.AddMinutes(11));

        var persisted = await app.WithDatabaseAsync(
            db => db.Tokens.AsNoTracking().SingleAsync());

        persisted.UserId.ShouldBe(user.Id);
        persisted.TokenHash.ShouldBe(TokenHasher.Hash(tokens.RefreshToken));
        persisted.TokenHash.ShouldNotBe(tokens.RefreshToken);
        persisted.TokenHash.Length.ShouldBe(64);
        persisted.Expiry.ShouldBeGreaterThan(beforeLogin.AddDays(6));
        persisted.Expiry.ShouldBeLessThan(beforeLogin.AddDays(8));
    }

    [Theory]
    [InlineData(EmailAddress, "wrong-password")]
    [InlineData("unknown-user@example.test", Password)]
    public async Task Login_WithInvalidCredentials_ReturnsUnauthorizedWithoutPersistingToken(
        string email,
        string password)
    {
        await app.SeedUserAsync(EmailAddress, Password);

        var (response, _) =
            await app.HttpsClient.POSTAsync<Login, LoginRequest, EmptyResponse>(
                new LoginRequest
                {
                    Email = email,
                    Password = password
                });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var tokenCount = await app.WithDatabaseAsync(db => db.Tokens.CountAsync());
        tokenCount.ShouldBe(0);
    }

    [Theory]
    [InlineData("", Password)]
    [InlineData(EmailAddress, "")]
    [InlineData(
        "email-address-that-is-deliberately-longer-than-the-configured-sixty-four-character-limit",
        Password)]
    [InlineData(
        EmailAddress,
        "password-that-is-deliberately-longer-than-the-configured-sixty-four-character-limit")]
    public async Task Login_WithInvalidBody_ReturnsBadRequestWithoutPersistingToken(
        string email,
        string password)
    {
        await app.SeedUserAsync(EmailAddress, Password);

        var (response, _) =
            await app.HttpsClient.POSTAsync<Login, LoginRequest, ErrorResponse>(
                new LoginRequest
                {
                    Email = email,
                    Password = password
                });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var tokenCount = await app.WithDatabaseAsync(db => db.Tokens.CountAsync());
        tokenCount.ShouldBe(0);
    }

    [Fact]
    public async Task Refresh_WithValidTokenAndNoUserId_RotatesToken()
    {
        var user = await app.SeedUserAsync(EmailAddress, Password);
        var originalTokens = await LoginAsync();

        var (response, renewedTokens) =
            await app.HttpsClient.POSTAsync<Refresh, TokenRequest, TokenResponse>(
                new TokenRequest
                {
                    RefreshToken = originalTokens.RefreshToken
                });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        renewedTokens.UserId.ShouldBe(user.Id);
        renewedTokens.RefreshToken.ShouldNotBe(originalTokens.RefreshToken);

        var (principal, jwt) = ValidateAccessToken(renewedTokens.AccessToken);
        principal.FindFirstValue("sub").ShouldBe(user.Id);
        principal.FindFirstValue("email").ShouldBe(user.Email);
        principal.FindFirstValue("role").ShouldBe(nameof(PlatformRole.User));
        jwt.ValidTo.ShouldBeGreaterThan(DateTime.UtcNow);

        var persistedTokens = await app.WithDatabaseAsync(
            db => db.Tokens.AsNoTracking().ToListAsync());

        persistedTokens.Count.ShouldBe(1);
        persistedTokens[0].TokenHash.ShouldBe(
            TokenHasher.Hash(renewedTokens.RefreshToken));
        persistedTokens[0].TokenHash.ShouldNotBe(
            TokenHasher.Hash(originalTokens.RefreshToken));
    }

    [Fact]
    public async Task Refresh_WithUnknownToken_ReturnsBadRequestAndKeepsCurrentToken()
    {
        await app.SeedUserAsync(EmailAddress, Password);
        var originalTokens = await LoginAsync();

        var (response, _) =
            await app.HttpsClient.POSTAsync<Refresh, TokenRequest, ErrorResponse>(
                new TokenRequest
                {
                    RefreshToken = "unknown-refresh-token"
                });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var persistedHashes = await app.WithDatabaseAsync(
            db => db.Tokens.Select(token => token.TokenHash).ToListAsync());

        persistedHashes.Count.ShouldBe(1);
        persistedHashes[0].ShouldBe(TokenHasher.Hash(originalTokens.RefreshToken));
    }

    [Fact]
    public async Task Refresh_WithExpiredToken_ReturnsBadRequestWithoutIssuingNewToken()
    {
        await app.SeedUserAsync(EmailAddress, Password);
        var originalTokens = await LoginAsync();

        await app.WithDatabaseAsync(
            async db =>
            {
                var storedToken = await db.Tokens.SingleAsync();
                storedToken.Expiry = DateTime.UtcNow.AddMinutes(-1);
                await db.SaveChangesAsync();
                return true;
            });

        var (response, _) =
            await app.HttpsClient.POSTAsync<Refresh, TokenRequest, ErrorResponse>(
                new TokenRequest
                {
                    RefreshToken = originalTokens.RefreshToken
                });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var tokenCount = await app.WithDatabaseAsync(db => db.Tokens.CountAsync());
        tokenCount.ShouldBe(1);
    }

    [Fact]
    public async Task Refresh_WhenOldTokenIsReused_ReturnsBadRequest()
    {
        await app.SeedUserAsync(EmailAddress, Password);
        var originalTokens = await LoginAsync();

        var (firstResponse, _) =
            await app.HttpsClient.POSTAsync<Refresh, TokenRequest, TokenResponse>(
                new TokenRequest
                {
                    RefreshToken = originalTokens.RefreshToken
                });
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var (reuseResponse, _) =
            await app.HttpsClient.POSTAsync<Refresh, TokenRequest, ErrorResponse>(
                new TokenRequest
                {
                    RefreshToken = originalTokens.RefreshToken
                });

        reuseResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var tokenCount = await app.WithDatabaseAsync(db => db.Tokens.CountAsync());
        tokenCount.ShouldBe(1);
    }

    [Fact]
    public async Task Refresh_WithMissingToken_ReturnsBadRequest()
    {
        await app.SeedUserAsync(EmailAddress, Password);

        var (response, _) =
            await app.HttpsClient.POSTAsync<Refresh, TokenRequest, ErrorResponse>(
                new TokenRequest
                {
                    RefreshToken = string.Empty
                });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var tokenCount = await app.WithDatabaseAsync(db => db.Tokens.CountAsync());
        tokenCount.ShouldBe(0);
    }

    [Fact]
    public async Task Refresh_WhenUserNoLongerExists_ReturnsBadRequest()
    {
        var user = await app.SeedUserAsync(EmailAddress, Password);
        var originalTokens = await LoginAsync();

        await app.WithDatabaseAsync(
            async db =>
            {
                var storedUser = await db.Users.SingleAsync(candidate => candidate.Id == user.Id);
                db.Users.Remove(storedUser);
                await db.SaveChangesAsync();
                return true;
            });

        var (response, _) =
            await app.HttpsClient.POSTAsync<Refresh, TokenRequest, ErrorResponse>(
                new TokenRequest
                {
                    RefreshToken = originalTokens.RefreshToken
                });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var persistedHashes = await app.WithDatabaseAsync(
            db => db.Tokens.Select(token => token.TokenHash).ToListAsync());
        persistedHashes.ShouldBe([TokenHasher.Hash(originalTokens.RefreshToken)]);
    }

    [Fact]
    public async Task RefreshingOneUser_DoesNotRevokeAnotherUsersToken()
    {
        var firstUser = await app.SeedUserAsync(EmailAddress, Password);
        var firstTokens = await LoginAsync();
        await app.SeedUserAsync("editor", "editor-password");
        var secondTokens = await LoginAsync("editor", "editor-password");

        var (response, renewedFirstTokens) =
            await app.HttpsClient.POSTAsync<Refresh, TokenRequest, TokenResponse>(
                new TokenRequest
                {
                    RefreshToken = firstTokens.RefreshToken
                });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        renewedFirstTokens.UserId.ShouldBe(firstUser.Id);

        var persistedHashes = await app.WithDatabaseAsync(
            db => db.Tokens.Select(token => token.TokenHash).ToListAsync());

        persistedHashes.Count.ShouldBe(2);
        persistedHashes.ShouldContain(TokenHasher.Hash(renewedFirstTokens.RefreshToken));
        persistedHashes.ShouldContain(TokenHasher.Hash(secondTokens.RefreshToken));
        persistedHashes.ShouldNotContain(TokenHasher.Hash(firstTokens.RefreshToken));
    }

    [Fact]
    public async Task Login_AsPlatformAdmin_IncludesPlatformAdminRoleInAccessToken()
    {
        await app.SeedUserAsync(EmailAddress, Password, PlatformRole.PlatformAdmin);

        var tokens = await LoginAsync();

        var (principal, _) = ValidateAccessToken(tokens.AccessToken);
        principal.FindFirstValue("role").ShouldBe(nameof(PlatformRole.PlatformAdmin));
    }

    [Fact]
    public async Task Refresh_UsesUsersCurrentRole()
    {
        var user = await app.SeedUserAsync(EmailAddress, Password);
        var originalTokens = await LoginAsync();

        await app.WithDatabaseAsync(
            async db =>
            {
                var storedUser = await db.Users.SingleAsync(candidate => candidate.Id == user.Id);
                storedUser.PlatformRole = PlatformRole.PlatformAdmin;
                await db.SaveChangesAsync();
                return true;
            });

        var (response, renewedTokens) =
            await app.HttpsClient.POSTAsync<Refresh, TokenRequest, TokenResponse>(
                new TokenRequest
                {
                    RefreshToken = originalTokens.RefreshToken
                });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var (principal, _) = ValidateAccessToken(renewedTokens.AccessToken);
        principal.FindFirstValue("role").ShouldBe(nameof(PlatformRole.PlatformAdmin));
    }

    [Fact]
    public async Task TenantInvitations_CreateScopedMemberships_AndCanOnlyBeUsedOnce()
    {
        var platformAdmin = await app.SeedUserAsync(
            EmailAddress,
            Password,
            PlatformRole.PlatformAdmin,
            "admin@example.com");

        var ownerInvitation =
            await app.WithServiceAsync<TenantInvitationService, CreatedTenantInvitation>(
                service => service.CreateTenantWithOwnerInvitationAsync(
                    platformAdmin.Id,
                    "Tenant A",
                    "owner@example.com"));

        var persistedInvitation = await app.WithDatabaseAsync(
            db => db.TenantInvitations.AsNoTracking().SingleAsync());

        persistedInvitation.Role.ShouldBe(TenantRole.Owner);
        persistedInvitation.Email.ShouldBe("owner@example.com");
        persistedInvitation.TokenHash.ShouldBe(TokenHasher.Hash(ownerInvitation.Token));
        persistedInvitation.TokenHash.ShouldNotBe(ownerInvitation.Token);
        persistedInvitation.ExpiresAt.ShouldBeGreaterThan(DateTime.UtcNow.AddHours(71));

        var owner = await app.SeedUserAsync(
            "owner",
            "owner-password",
            email: "owner@example.com");

        var ownerMembership =
            await app.WithServiceAsync<TenantInvitationService, TenantMembership>(
                service => service.AcceptInvitationAsync(owner.Id, ownerInvitation.Token));

        ownerMembership.TenantId.ShouldBe(ownerInvitation.TenantId);
        ownerMembership.Role.ShouldBe(TenantRole.Owner);

        var editorInvitation =
            await app.WithServiceAsync<TenantInvitationService, CreatedTenantInvitation>(
                service => service.CreateInvitationAsync(
                    owner.Id,
                    ownerInvitation.TenantId,
                    "editor@example.com",
                    TenantRole.Editor));

        var editor = await app.SeedUserAsync(
            "editor",
            "editor-password",
            email: "editor@example.com");

        await app.WithServiceAsync<TenantInvitationService, TenantMembership>(
            service => service.AcceptInvitationAsync(editor.Id, editorInvitation.Token));

        var editorMembership =
            await app.WithServiceAsync<TenantAccessService, TenantMembership?>(
                service => service.FindMembershipAsync(
                    new ClaimsPrincipal(
                        new ClaimsIdentity([new Claim("sub", editor.Id)])),
                    ownerInvitation.TenantId));

        editorMembership.ShouldNotBeNull();
        editorMembership.Role.ShouldBe(TenantRole.Editor);

        var unrelatedMembership =
            await app.WithServiceAsync<TenantAccessService, TenantMembership?>(
                service => service.FindMembershipAsync(
                    new ClaimsPrincipal(
                        new ClaimsIdentity([new Claim("sub", editor.Id)])),
                    Guid.NewGuid()));

        unrelatedMembership.ShouldBeNull();

        await Should.ThrowAsync<InvitationFlowException>(
            () => app.WithServiceAsync<TenantInvitationService, TenantMembership>(
                service => service.AcceptInvitationAsync(editor.Id, editorInvitation.Token)));
    }

    [Fact]
    public async Task RegularPlatformUser_CannotCreateTenant()
    {
        var user = await app.SeedUserAsync(EmailAddress, Password);

        await Should.ThrowAsync<InvitationFlowException>(
            () => app.WithServiceAsync<TenantInvitationService, CreatedTenantInvitation>(
                service => service.CreateTenantWithOwnerInvitationAsync(
                    user.Id,
                    "Tenant A",
                    "owner@example.com")));
    }

    private async Task<TokenResponse> LoginAsync(
        string email = EmailAddress,
        string password = Password)
    {
        var (response, tokens) =
            await app.HttpsClient.POSTAsync<Login, LoginRequest, TokenResponse>(
                new LoginRequest
                {
                    Email = TestApp.AsEmail(email),
                    Password = password
                });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return tokens;
    }

    private static (ClaimsPrincipal Principal, JwtSecurityToken Token) ValidateAccessToken(
        string accessToken)
    {
        var handler = new JwtSecurityTokenHandler
        {
            MapInboundClaims = false
        };

        var parameters = new TokenValidationParameters
        {
            ClockSkew = TimeSpan.Zero,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(TestApp.SigningKey)),
            RequireExpirationTime = true,
            ValidateAudience = false,
            ValidateIssuer = false,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
        };

        var principal = handler.ValidateToken(
            accessToken,
            parameters,
            out var validatedToken);

        return (principal, (JwtSecurityToken)validatedToken);
    }
}
