using FastEndpoints;
using FastEndpoints.Security;
using FluentValidation;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;

using HeadlessCms.Api.Documentation;

namespace HeadlessCms.Api.Endpoints.Auth;

public sealed class AcceptInvitation(WorkspaceInvitationService invitations)
    : Endpoint<AcceptInvitation.Request, AcceptInvitation.ResponseDto>
{
    public override void Configure()
    {
        Post("auth/invitations/accept");
        Claims("sub");
        Description(b => b.WithTags("Authentication"), clearDefaults: true);
        Summary((EndpointSummary s) =>
        {
            s.Summary = "Accept an invitation";
            s.Description = "Access: Authenticated user whose email matches the invitation.\n\nAdds membership for an existing account. Already accepted invitations or existing memberships return 409; expired or revoked invitations return 410.";
            s.Params["Token"] = "Single-use invitation token from the invitation link; maximum 512 characters.";
            s.ExampleRequest = new { Token = "EXAMPLE_TOKEN" };
            s.Response<AcceptInvitation.ResponseDto>(200, "Success.");
            s.ResponseExamples[200] = ApiExamples.Membership;
            s.Response<FastEndpoints.ProblemDetails>(400, "Invalid request or validation failure.", "application/problem+json");
            s.Response<ApiProblem>(401, "Authentication is required, or the session has expired.", "application/problem+json");
            s.Response<ApiProblem>(403, "The caller does not have permission.", "application/problem+json");
            s.Response<ApiProblem>(404, "Resource not found or inaccessible.", "application/problem+json");
            s.Response<ApiProblem>(409, "Conflicting membership or invitation state.", "application/problem+json");
            s.Response<ApiProblem>(410, "Invitation expired or revoked.", "application/problem+json");
        });
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
