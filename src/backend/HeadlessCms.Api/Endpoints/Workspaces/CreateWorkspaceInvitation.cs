using FastEndpoints;
using FastEndpoints.Security;
using FluentValidation;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;

namespace HeadlessCms.Api.Endpoints.Workspaces;

public sealed class CreateWorkspaceInvitation(
    WorkspaceInvitationService invitations)
    : Endpoint<CreateWorkspaceInvitationRequest, WorkspaceInvitationResponse>
{
    public override void Configure()
    {
        Post("workspaces/{workspaceId:guid}/invitations");
        Claims("sub");
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
