using FastEndpoints;
using HeadlessCms.Api.Workspaces.Services;

namespace HeadlessCms.Api.Endpoints.Workspaces;

public sealed class RevokeWorkspaceInvitation(
    WorkspaceInvitationService invitations)
    : Endpoint<RevokeWorkspaceInvitationRequest>
{
    public override void Configure()
    {
        Delete("workspaces/{workspaceId:guid}/invitations/{invitationId:guid}");
        Claims("sub");
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
