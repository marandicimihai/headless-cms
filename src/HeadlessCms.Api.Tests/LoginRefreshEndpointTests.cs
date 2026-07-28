using System.Net;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FastEndpoints;
using FastEndpoints.Security;
using FastEndpoints.Testing;
using HeadlessCms.Api.Auth.Services;
using HeadlessCms.Api.Endpoints.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Shouldly;
using Xunit;

namespace HeadlessCms.Api.Tests;

public sealed class LoginRefreshEndpointTests(AuthApp app) : TestBase<AuthApp>
{
    private const string Username = "admin";
    private const string Password = "correct-password";

    protected override async ValueTask SetupAsync()
    {
        await app.ResetDatabaseAsync();
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsAndPersistsTokenPair()
    {
        var user = await app.SeedUserAsync(Username, Password);
        var beforeLogin = DateTime.UtcNow;

        var (response, tokens) =
            await app.HttpsClient.POSTAsync<Login, LoginRequest, TokenResponse>(
                new LoginRequest
                {
                    Username = Username,
                    Password = Password
                });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        tokens.UserId.ShouldBe(user.Id);
        tokens.AccessToken.ShouldNotBeNullOrWhiteSpace();
        tokens.RefreshToken.ShouldNotBeNullOrWhiteSpace();

        var (principal, jwt) = ValidateAccessToken(tokens.AccessToken);
        principal.FindFirstValue("sub").ShouldBe(user.Id);
        principal.FindFirstValue("username").ShouldBe(user.Username);
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
    [InlineData(Username, "wrong-password")]
    [InlineData("unknown-user", Password)]
    public async Task Login_WithInvalidCredentials_ReturnsUnauthorizedWithoutPersistingToken(
        string username,
        string password)
    {
        await app.SeedUserAsync(Username, Password);

        var (response, _) =
            await app.HttpsClient.POSTAsync<Login, LoginRequest, EmptyResponse>(
                new LoginRequest
                {
                    Username = username,
                    Password = password
                });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var tokenCount = await app.WithDatabaseAsync(db => db.Tokens.CountAsync());
        tokenCount.ShouldBe(0);
    }

    [Theory]
    [InlineData("", Password)]
    [InlineData(Username, "")]
    [InlineData(
        "username-that-is-deliberately-longer-than-the-configured-sixty-four-character-limit",
        Password)]
    [InlineData(
        Username,
        "password-that-is-deliberately-longer-than-the-configured-sixty-four-character-limit")]
    public async Task Login_WithInvalidBody_ReturnsBadRequestWithoutPersistingToken(
        string username,
        string password)
    {
        await app.SeedUserAsync(Username, Password);

        var (response, _) =
            await app.HttpsClient.POSTAsync<Login, LoginRequest, ErrorResponse>(
                new LoginRequest
                {
                    Username = username,
                    Password = password
                });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var tokenCount = await app.WithDatabaseAsync(db => db.Tokens.CountAsync());
        tokenCount.ShouldBe(0);
    }

    [Fact]
    public async Task Refresh_WithValidTokenAndNoUserId_RotatesToken()
    {
        var user = await app.SeedUserAsync(Username, Password);
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
        principal.FindFirstValue("username").ShouldBe(user.Username);
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
        await app.SeedUserAsync(Username, Password);
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
        await app.SeedUserAsync(Username, Password);
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
        await app.SeedUserAsync(Username, Password);
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
        await app.SeedUserAsync(Username, Password);

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
        var user = await app.SeedUserAsync(Username, Password);
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
        var firstUser = await app.SeedUserAsync(Username, Password);
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

    private async Task<TokenResponse> LoginAsync(
        string username = Username,
        string password = Password)
    {
        var (response, tokens) =
            await app.HttpsClient.POSTAsync<Login, LoginRequest, TokenResponse>(
                new LoginRequest
                {
                    Username = username,
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
                Encoding.UTF8.GetBytes(AuthApp.SigningKey)),
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
