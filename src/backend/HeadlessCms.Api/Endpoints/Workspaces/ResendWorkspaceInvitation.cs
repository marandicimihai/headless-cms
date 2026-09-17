using FastEndpoints;
using HeadlessCms.Api.Workspaces.Services;

namespace HeadlessCms.Api.Endpoints.Workspaces;

public sealed class ResendWorkspaceInvitation(
    WorkspaceInvitationService invitations)
    : EndpointWithoutRequest<WorkspaceInvitationResponse>
{
    public override void Configure()
    {
        Post("workspaces/{workspaceId:guid}/invitations/{invitationId:guid}/resend");
        Claims("sub");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var workspaceId = Route<Guid>("workspaceId");
        var invitationId = Route<Guid>("invitationId");
        var invitation = await invitations.FindManageableAsync(User, workspaceId, invitationId, ct);
        if (invitation is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        try
        {
            await invitations.ResendAsync(invitation, ct);
            Response = WorkspaceInvitationResponse.FromInvitation(invitation);
        }
        catch (InvitationFlowException exception)
        {
            await ApiErrors.SendAsync(HttpContext, exception, ct);
        }
    }

}
