using FastEndpoints;
using HeadlessCms.Api.Workspaces.Services;

using HeadlessCms.Api.Documentation;

namespace HeadlessCms.Api.Endpoints.Workspaces;

public sealed class RevokeWorkspaceInvitation(
    WorkspaceInvitationService invitations)
    : Endpoint<RevokeWorkspaceInvitationRequest>
{
    public override void Configure()
    {
        Delete("workspaces/{workspaceId:guid}/invitations/{invitationId:guid}");
        Claims("sub");
        Description(b => b.WithTags("Invitations"), clearDefaults: true);
        Summary((EndpointSummary s) =>
        {
            s.Summary = "Revoke an invitation";
            s.Description = "Access: Workspace Owner; PlatformAdmin may manage owner invitations only in an ownerless workspace.\n\nRevokes the invitation. Accepted invitations return 409. Revoking an already revoked invitation succeeds.";
            s.Params["WorkspaceId"] = "Workspace UUID.";
            s.Params["InvitationId"] = "Invitation UUID.";
            s.Response(204, "No content.");
            s.Response<FastEndpoints.ProblemDetails>(400, "Invalid request or validation failure.", "application/problem+json");
            s.Response<ApiProblem>(401, "Authentication is required, or the session has expired.", "application/problem+json");
            s.Response(404, "Resource not found or inaccessible.");
            s.Response<ApiProblem>(409, "Conflicting membership or invitation state.", "application/problem+json");
        });
    }

    public override async Task HandleAsync(
        RevokeWorkspaceInvitationRequest request,
        CancellationToken ct)
    {
        var invitation = await invitations.FindManageableAsync(User, request.WorkspaceId, request.InvitationId, ct);
        if (invitation is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        try
        {
            await invitations.RevokeAsync(invitation, ct);
            await Send.NoContentAsync(ct);
        }
        catch (InvitationFlowException exception)
        {
            await ApiErrors.SendAsync(HttpContext, exception, ct);
        }
    }

}

public sealed class RevokeWorkspaceInvitationRequest
{
    public Guid WorkspaceId { get; init; }
    public Guid InvitationId { get; init; }
}
