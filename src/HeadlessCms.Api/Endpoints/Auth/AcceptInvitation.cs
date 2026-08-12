using FastEndpoints;
using FastEndpoints.Security;
using FluentValidation;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;

namespace HeadlessCms.Api.Endpoints.Auth;

public sealed class AcceptInvitation(WorkspaceInvitationService invitations)
    : Endpoint<AcceptInvitation.Request, AcceptInvitation.ResponseDto>
{
    public override void Configure()
    {
        Post("auth/invitations/accept");
        Claims("sub");
    }

    public override async Task HandleAsync(Request request, CancellationToken ct)
    {
        try
        {
            var membership = await invitations.AcceptInvitationAsync(
                User.ClaimValue("sub")!,
                request.Token,
                ct);
            var workspaceName = membership.Workspace?.Name;
            Response = new ResponseDto(
                membership.WorkspaceId,
                workspaceName ?? string.Empty,
                membership.Role,
                membership.JoinedAt);
        }
        catch (InvitationFlowException exception)
        {
            await ApiErrors.SendAsync(HttpContext, exception, ct);
        }
    }

    public sealed class Request
    {
        public required string Token { get; init; }
    }

    public sealed record ResponseDto(
        Guid WorkspaceId,
        string WorkspaceName,
        WorkspaceRole Role,
        DateTime JoinedAt);

    public sealed class RequestValidator : Validator<Request>
    {
        public RequestValidator() =>
            RuleFor(request => request.Token).NotEmpty().MaximumLength(512);
    }
}
