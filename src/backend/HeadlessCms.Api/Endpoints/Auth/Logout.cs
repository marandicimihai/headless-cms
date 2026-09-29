using FastEndpoints;
using HeadlessCms.Api.Auth.Services;

using HeadlessCms.Api.Documentation;

namespace HeadlessCms.Api.Endpoints.Auth;

public sealed class Logout(AuthSessionService sessions) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("auth/logout");
        AllowAnonymous();
        Description(b => b.WithTags("Authentication"), clearDefaults: true);
        Summary((EndpointSummary s) =>
        {
            s.Summary = "Log out";
            s.Description = "Access: Anonymous; presented session is revoked if valid.\n\nIdempotently revokes the current session and clears its cookie. Other sessions are unaffected.";
            s.Response(204, "No content.");
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await sessions.RevokeAsync(
            HttpContext.Request.Cookies[AuthSessionService.CookieName],
            ct);
        sessions.DeleteCookie(HttpContext.Response);
        await Send.NoContentAsync(ct);
    }
}
