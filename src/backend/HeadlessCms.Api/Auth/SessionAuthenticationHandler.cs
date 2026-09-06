using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using HeadlessCms.Api.Auth.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace HeadlessCms.Api.Auth;

public static class SessionAuthenticationDefaults
{
    public const string Scheme = "DatabaseSession";
}

public sealed class SessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    AuthSessionService sessions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var secret = Request.Cookies[AuthSessionService.CookieName];
        var session = await sessions.ResolveAsync(secret, Context.RequestAborted);

        if (session is null)
            return AuthenticateResult.NoResult();

        Context.Items[AuthSessionService.HttpContextItemName] = session;
        var claims = new[]
        {
            new Claim("sub", session.UserId),
            new Claim("email", session.Email),
            new Claim("role", session.PlatformRole.ToString())
        };
        var identity = new ClaimsIdentity(
            claims,
            Scheme.Name,
            "email",
            "role");
        var principal = new ClaimsPrincipal(identity);

        return AuthenticateResult.Success(
            new AuthenticationTicket(principal, Scheme.Name));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties) =>
        WriteProblemAsync(
            StatusCodes.Status401Unauthorized,
            "unauthorized",
            "Authentication is required.");

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) =>
        WriteProblemAsync(
            StatusCodes.Status403Forbidden,
            "forbidden",
            "You do not have permission to perform this action.");

    private Task WriteProblemAsync(int status, string code, string detail)
    {
        Response.StatusCode = status;
        Response.ContentType = "application/problem+json";
        return Response.WriteAsync(
            JsonSerializer.Serialize(
                new
                {
                    type = $"https://httpstatuses.com/{status}",
                    title = status == StatusCodes.Status401Unauthorized
                        ? "Unauthorized"
                        : "Forbidden",
                    status,
                    detail,
                    code
                }),
            Context.RequestAborted);
    }
}
