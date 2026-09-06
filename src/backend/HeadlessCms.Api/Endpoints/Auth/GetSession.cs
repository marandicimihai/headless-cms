using FastEndpoints;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Auth.Services;

namespace HeadlessCms.Api.Endpoints.Auth;

public sealed record AuthSessionResponse(
    string UserId,
    string Email,
    PlatformRole PlatformRole,
    DateTime IdleExpiresAt,
    DateTime AbsoluteExpiresAt);

public sealed class GetSession : EndpointWithoutRequest<AuthSessionResponse>
{
    public override void Configure()
    {
        Get("auth/session");
        Claims("sub");
    }

    public override Task HandleAsync(CancellationToken ct)
    {
        var session = (ResolvedAuthSession)
            HttpContext.Items[AuthSessionService.HttpContextItemName]!;
        Response = new AuthSessionResponse(
            session.UserId,
            session.Email,
            session.PlatformRole,
            session.IdleExpiresAt,
            session.AbsoluteExpiresAt);
        return Task.CompletedTask;
    }
}
