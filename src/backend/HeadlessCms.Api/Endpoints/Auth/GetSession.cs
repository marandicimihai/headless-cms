using FastEndpoints;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Auth.Services;

using HeadlessCms.Api.Documentation;

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
        Description(b => b.WithTags("Authentication"), clearDefaults: true);
        Summary((EndpointSummary s) =>
        {
            s.Summary = "Get current session";
            s.Description = "Access: Authenticated user.\n\nReturns the current user's identity, platform role, and idle and absolute session expiry timestamps.";
            s.Response<AuthSessionResponse>(200, "Success.");
            s.ResponseExamples[200] = ApiExamples.Session;
            s.Response<ApiProblem>(401, "Authentication is required, or the session has expired.", "application/problem+json");
        });
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
