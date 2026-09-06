using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FastEndpoints;
using FastEndpoints.Testing;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Auth.Services;
using HeadlessCms.Api.Endpoints.Auth;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace HeadlessCms.Api.Tests;

[Collection<TestAppCollection>]
public sealed class LoginSessionEndpointTests(TestApp app) : TestBase
{
    private const string EmailAddress = "admin@example.test";
    private const string Password = "correct-password";
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    protected override async ValueTask SetupAsync() =>
        await app.ResetDatabaseAsync();

    [Fact]
    public async Task Login_WithValidCredentials_CreatesHashedSessionAndSecureCookie()
    {
        var user = await app.SeedUserAsync(EmailAddress, Password);
        var beforeLogin = DateTime.UtcNow;

        var (response, body) =
            await app.HttpsClient.POSTAsync<Login, LoginRequest, AuthSessionResponse>(
                new LoginRequest
                {
                    Email = EmailAddress.ToUpperInvariant(),
                    Password = Password
                });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        body.UserId.ShouldBe(user.Id);
        body.Email.ShouldBe(user.Email);
        body.PlatformRole.ShouldBe(PlatformRole.User);
        body.IdleExpiresAt.ShouldBeInRange(beforeLogin.AddDays(29), beforeLogin.AddDays(31));
        body.AbsoluteExpiresAt.ShouldBeInRange(beforeLogin.AddDays(364), beforeLogin.AddDays(366));

        var setCookie = response.Headers.GetValues("Set-Cookie").Single();
        setCookie.ShouldContain("cms_session=");
        setCookie.ShouldContain("httponly", Case.Insensitive);
        setCookie.ShouldContain("secure", Case.Insensitive);
        setCookie.ShouldContain("samesite=lax", Case.Insensitive);
        setCookie.ShouldContain("path=/", Case.Insensitive);
        setCookie.ShouldContain("expires=", Case.Insensitive);
        setCookie.ShouldNotContain("domain=", Case.Insensitive);

        var cookie = TestApp.ExtractSessionCookie(response);
        var secret = cookie[(cookie.IndexOf('=') + 1)..];
        var persisted = await app.WithDatabaseAsync(
            db => db.AuthSessions.AsNoTracking().SingleAsync());

        persisted.UserId.ShouldBe(user.Id);
        persisted.SecretHash.ShouldBe(TokenHasher.Hash(secret));
        persisted.SecretHash.ShouldNotBe(secret);
        persisted.SecretHash.Length.ShouldBe(64);
    }

    [Theory]
    [InlineData(EmailAddress, "wrong-password")]
    [InlineData("unknown-user@example.test", Password)]
    public async Task Login_WithInvalidCredentials_ReturnsUnauthorizedWithoutSession(
        string email,
        string password)
    {
        await app.SeedUserAsync(EmailAddress, Password);
        using var response = await app.HttpsClient.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await app.WithDatabaseAsync(db => db.AuthSessions.CountAsync())).ShouldBe(0);
    }

    [Fact]
    public async Task Session_WithValidCookie_ReturnsCurrentUserAndCurrentRole()
    {
        var user = await app.SeedUserAsync(EmailAddress, Password);
        var cookie = await app.LoginAsync(user.Email, Password);

        await app.WithDatabaseAsync(async db =>
        {
            var storedUser = await db.Users.SingleAsync(candidate => candidate.Id == user.Id);
            storedUser.PlatformRole = PlatformRole.PlatformAdmin;
            await db.SaveChangesAsync();
            return true;
        });

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/session");
        request.Headers.Add("Cookie", cookie);
        using var response = await app.HttpsClient.SendAsync(
            request,
            TestContext.Current.CancellationToken);
        var session = await response.Content.ReadFromJsonAsync<AuthSessionResponse>(
            JsonOptions,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        session.ShouldNotBeNull();
        session.UserId.ShouldBe(user.Id);
        session.PlatformRole.ShouldBe(PlatformRole.PlatformAdmin);
    }

    [Theory]
    [InlineData("cms_session=tampered")]
    [InlineData("cms_session=unknown")]
    public async Task Session_WithUnknownOrTamperedCookie_ReturnsUnauthorized(string cookie)
    {
        await app.SeedUserAsync(EmailAddress, Password);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/session");
        request.Headers.Add("Cookie", cookie);
        using var response = await app.HttpsClient.SendAsync(
            request,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Session_WithIdleOrAbsoluteExpiry_ReturnsUnauthorized(bool expireIdle)
    {
        var user = await app.SeedUserAsync(EmailAddress, Password);
        var cookie = await app.LoginAsync(user.Email, Password);
        await app.WithDatabaseAsync(async db =>
        {
            var session = await db.AuthSessions.SingleAsync();
            if (expireIdle)
                session.IdleExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            else
                session.AbsoluteExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
            return true;
        });

        (await GetSessionAsync(cookie)).ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Session_RenewsAtMostDailyAndCapsIdleExpiryAtAbsoluteExpiry()
    {
        var user = await app.SeedUserAsync(EmailAddress, Password);
        var cookie = await app.LoginAsync(user.Email, Password);
        var absoluteExpiry = DateTime.UtcNow.AddDays(10);
        await app.WithDatabaseAsync(async db =>
        {
            var session = await db.AuthSessions.SingleAsync();
            session.LastSeenAt = DateTime.UtcNow.AddHours(-25);
            session.IdleExpiresAt = DateTime.UtcNow.AddDays(1);
            session.AbsoluteExpiresAt = absoluteExpiry;
            await db.SaveChangesAsync();
            return true;
        });

        await GetSessionAsync(cookie);
        var renewed = await app.WithDatabaseAsync(
            db => db.AuthSessions.AsNoTracking().SingleAsync());
        renewed.IdleExpiresAt.ShouldBe(absoluteExpiry, TimeSpan.FromSeconds(1));
        var firstLastSeen = renewed.LastSeenAt;

        await GetSessionAsync(cookie);
        var secondLastSeen = await app.WithDatabaseAsync(
            db => db.AuthSessions.Select(session => session.LastSeenAt).SingleAsync());
        secondLastSeen.ShouldBe(firstLastSeen);
    }

    [Fact]
    public async Task EleventhLogin_EvictsLeastRecentlyUsedSession()
    {
        var user = await app.SeedUserAsync(EmailAddress, Password);
        var sessionCookies = new List<string>();

        for (var index = 0; index < 11; index++)
            sessionCookies.Add(await app.LoginAsync(user.Email, Password));

        (await app.WithDatabaseAsync(db => db.AuthSessions.CountAsync()))
            .ShouldBe(AuthSessionService.MaximumActiveSessions);
        (await GetSessionAsync(sessionCookies[0])).ShouldBe(HttpStatusCode.Unauthorized);
        (await GetSessionAsync(sessionCookies[^1])).ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Logout_RevokesOnlyPresentedSessionAndIsIdempotent()
    {
        var user = await app.SeedUserAsync(EmailAddress, Password);
        var first = await app.LoginAsync(user.Email, Password);
        var second = await app.LoginAsync(user.Email, Password);

        using var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        logoutRequest.Headers.Add("Cookie", first);
        using var logout = await app.HttpsClient.SendAsync(
            logoutRequest,
            TestContext.Current.CancellationToken);
        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        logout.Headers.GetValues("Set-Cookie").Single().ShouldContain("cms_session=");

        (await GetSessionAsync(first)).ShouldBe(HttpStatusCode.Unauthorized);
        (await GetSessionAsync(second)).ShouldBe(HttpStatusCode.OK);

        using var repeated = await app.HttpsClient.PostAsync(
            "/api/auth/logout",
            null,
            TestContext.Current.CancellationToken);
        repeated.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeletingUser_CascadesSessions()
    {
        var user = await app.SeedUserAsync(EmailAddress, Password);
        await app.LoginAsync(user.Email, Password);

        await app.WithDatabaseAsync(async db =>
        {
            db.Users.Remove(await db.Users.SingleAsync());
            await db.SaveChangesAsync();
            return true;
        });

        (await app.WithDatabaseAsync(db => db.AuthSessions.CountAsync())).ShouldBe(0);
    }

    [Fact]
    public async Task RefreshEndpoint_IsNoLongerExposed()
    {
        using var response = await app.HttpsClient.PostAsJsonAsync(
            "/api/auth/refresh",
            new { refreshToken = "legacy" },
            TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task InvitationRegistration_CreatesSignedInSessionWithoutTokenPayload()
    {
        var owner = await app.SeedUserAsync("owner", "owner-password");
        var workspace = await app.SeedWorkspaceAsync((owner, WorkspaceRole.Owner));
        var invitation = await app.WithServiceAsync<
            WorkspaceInvitationService,
            CreatedWorkspaceInvitation>(service => service.CreateInvitationAsync(
                owner.Id,
                workspace.Id,
                "new-user@example.test",
                WorkspaceRole.Editor));

        using var registration = await app.HttpsClient.PostAsJsonAsync(
            "/api/auth/invitations/register",
            new { token = invitation.Token, password = "new-user-password" },
            TestContext.Current.CancellationToken);

        registration.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await registration.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);
        json.ShouldNotContain("tokens", Case.Insensitive);
        var cookie = TestApp.ExtractSessionCookie(registration);
        (await GetSessionAsync(cookie)).ShouldBe(HttpStatusCode.OK);
    }

    private async Task<HttpStatusCode> GetSessionAsync(string cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/session");
        request.Headers.Add("Cookie", cookie);
        using var response = await app.HttpsClient.SendAsync(
            request,
            TestContext.Current.CancellationToken);
        return response.StatusCode;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
