using FastEndpoints;
using FluentValidation;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;

using HeadlessCms.Api.Documentation;

namespace HeadlessCms.Api.Endpoints.Auth;

public sealed class PreviewInvitation(WorkspaceInvitationService invitations)
    : Endpoint<PreviewInvitation.Request, PreviewInvitation.ResponseDto>
{
    public override void Configure()
    {
        Post("auth/invitations/preview");
        AllowAnonymous();
        Description(b => b.WithTags("Authentication"), clearDefaults: true);
        Summary((EndpointSummary s) =>
        {
            s.Summary = "Preview an invitation";
            s.Description = "Access: Anonymous with a valid invitation token.\n\nReturns the workspace, invited role, masked email, and expiry. Tokens are single-use; expired or revoked invitations return 410.";
            s.Params["Token"] = "Single-use invitation token from the invitation link; maximum 512 characters.";
            s.ExampleRequest = new { Token = "EXAMPLE_TOKEN" };
            s.Response<PreviewInvitation.ResponseDto>(200, "Success.");
            s.ResponseExamples[200] = new { WorkspaceName = "Editorial", MaskedEmail = "e*****@example.com", Role = "editor", ExpiresAt = "2026-09-28T12:00:00Z" };
            s.Response<FastEndpoints.ProblemDetails>(400, "Invalid request or validation failure.", "application/problem+json");
            s.Response<ApiProblem>(404, "Resource not found or inaccessible.", "application/problem+json");
            s.Response<ApiProblem>(409, "Conflicting membership or invitation state.", "application/problem+json");
            s.Response<ApiProblem>(410, "Invitation expired or revoked.", "application/problem+json");
        });
    }

    public override async Task HandleAsync(Request request, CancellationToken ct)
    {
        try
        {
            var invitation = await invitations.PreviewAsync(request.Token, ct);
            Response = new ResponseDto(
                invitation.WorkspaceName,
                Mask(invitation.Email),
                invitation.Role,
                invitation.ExpiresAt);
        }
        catch (InvitationFlowException exception)
        {
            await ApiErrors.SendAsync(HttpContext, exception, ct);
        }
    }

    private static string Mask(string email)
    {
        var separator = email.IndexOf('@');
        var local = email[..separator];
        var maskedLocal = local.Length <= 1
            ? "*"
            : $"{local[0]}{new string('*', Math.Min(local.Length - 1, 6))}";
        return $"{maskedLocal}{email[separator..]}";
    }

    public sealed class Request
    {
        public required string Token { get; init; }
    }

    public sealed record ResponseDto(
        string WorkspaceName,
        string MaskedEmail,
        WorkspaceRole Role,
        DateTime ExpiresAt);

    public sealed class RequestValidator : Validator<Request>
    {
        public RequestValidator() =>
            RuleFor(request => request.Token).NotEmpty().MaximumLength(512);
    }
}
