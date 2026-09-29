using FastEndpoints;
using FastEndpoints.Security;
using FluentValidation;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;

using HeadlessCms.Api.Documentation;

namespace HeadlessCms.Api.Endpoints.Workspaces;

public sealed class CreateWorkspaceInvitation(
    WorkspaceInvitationService invitations)
    : Endpoint<CreateWorkspaceInvitationRequest, WorkspaceInvitationResponse>
{
    public override void Configure()
    {
        Post("workspaces/{workspaceId:guid}/invitations");
        Claims("sub");
        Description(b => b.WithTags("Invitations"), clearDefaults: true);
        Summary((EndpointSummary s) =>
        {
            s.Summary = "Create an invitation";
            s.Description = "Access: Workspace Owner.\n\nInvites an editor or member by email. Returns invitationUrl only on creation or resend; copy the link to share it. Existing membership or a pending invitation returns 409.";
            s.Params["WorkspaceId"] = "Workspace UUID.";
            s.Params["Email"] = "Email address (maximum 320 characters).";
            s.Params["Role"] = "The role to assign: editor or member.";
            s.ExampleRequest = new { Email = "editor@example.com", Role = "editor" };
            s.Response<WorkspaceInvitationResponse>(201, "Created.");
            s.ResponseExamples[201] = ApiExamples.InvitationWithLink;
            s.Response<FastEndpoints.ProblemDetails>(400, "Invalid request or validation failure.", "application/problem+json");
            s.Response<ApiProblem>(401, "Authentication is required, or the session has expired.", "application/problem+json");
            s.Response<ApiProblem>(403, "The caller does not have permission.", "application/problem+json");
            s.Response<ApiProblem>(404, "Resource not found or inaccessible.", "application/problem+json");
            s.Response<ApiProblem>(409, "Conflicting membership or invitation state.", "application/problem+json");
        });
    }

    public override async Task HandleAsync(
        CreateWorkspaceInvitationRequest request,
        CancellationToken ct)
    {
        try
        {
            var created = await invitations.CreateInvitationAsync(
                User.ClaimValue("sub")!,
                request.WorkspaceId,
                request.Email,
                request.Role,
                ct);
            HttpContext.Response.Headers.CacheControl = "no-store";
            await Send.ResponseAsync(
                WorkspaceInvitationResponse.FromInvitation(created.Invitation) with
                { InvitationUrl = invitations.GetInvitationUrl(created.Token) },
                StatusCodes.Status201Created,
                ct);
        }
        catch (InvitationFlowException exception)
        {
            await ApiErrors.SendAsync(HttpContext, exception, ct);
        }
    }
}

public sealed class CreateWorkspaceInvitationRequest
{
    public Guid WorkspaceId { get; init; }
    public required string Email { get; init; }
    public WorkspaceRole Role { get; init; }
}

public sealed class CreateWorkspaceInvitationRequestValidator
    : Validator<CreateWorkspaceInvitationRequest>
{
    public CreateWorkspaceInvitationRequestValidator()
    {
        RuleFor(request => request.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(request => request.Role)
            .Must(role => role is WorkspaceRole.Editor or WorkspaceRole.Member)
            .WithMessage("Role must be Editor or Member.");
    }
}
