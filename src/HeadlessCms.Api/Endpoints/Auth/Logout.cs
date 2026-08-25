using FastEndpoints;
using HeadlessCms.Api.Auth.Services;

namespace HeadlessCms.Api.Endpoints.Auth;

public sealed class Logout(AuthSessionService sessions) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("auth/logout");
        AllowAnonymous();
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
