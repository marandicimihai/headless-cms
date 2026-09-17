using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;

namespace HeadlessCms.Api.Endpoints.Workspaces;

public sealed record WorkspaceInvitationResponse(
    Guid Id,
    Guid WorkspaceId,
    string Email,
    WorkspaceRole Role,
    InvitationStatus Status,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    DateTime? LastSentAt,
    DateTime? AcceptedAt,
    DateTime? RevokedAt)
{
    public static WorkspaceInvitationResponse FromInvitation(WorkspaceInvitation invitation) =>
        new(invitation.Id, invitation.WorkspaceId, invitation.Email, invitation.Role,
            WorkspaceInvitationService.GetStatus(invitation), invitation.CreatedAt,
            invitation.ExpiresAt, invitation.LastSentAt, invitation.AcceptedAt, invitation.RevokedAt);
}
