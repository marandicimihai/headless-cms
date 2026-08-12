using FastEndpoints;
using FastEndpoints.Security;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Endpoints.Workspaces;

public sealed class ResendWorkspaceInvitation(
    ApplicationDbContext db,
    WorkspaceInvitationService invitations)
    : EndpointWithoutRequest<ResendWorkspaceInvitationResponse>
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
        var invitation = await FindManageableAsync(workspaceId, invitationId, ct);
        if (invitation is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        try
        {
            await invitations.ResendAsync(invitation, ct);
            Response = ToResponse(invitation);
        }
        catch (InvitationFlowException exception)
        {
            await ApiErrors.SendAsync(HttpContext, exception, ct);
        }
    }

    private async Task<WorkspaceInvitation?> FindManageableAsync(
        Guid workspaceId,
        Guid invitationId,
        CancellationToken ct)
    {
        var invitation = await db.WorkspaceInvitations.SingleOrDefaultAsync(
            candidate =>
                candidate.Id == invitationId &&
                candidate.WorkspaceId == workspaceId,
            ct);
        if (invitation is null)
            return null;

        var userId = User.ClaimValue("sub")!;
        var isOwner = await db.WorkspaceMemberships.AnyAsync(
            membership =>
                membership.WorkspaceId == workspaceId &&
                membership.UserId == userId &&
                membership.Role == WorkspaceRole.Owner,
            ct);
        if (isOwner)
            return invitation;

        if (!User.IsInRole(nameof(PlatformRole.PlatformAdmin)) ||
            invitation.Role != WorkspaceRole.Owner)
        {
            return null;
        }

        var workspaceHasOwner = await db.WorkspaceMemberships.AnyAsync(
            membership =>
                membership.WorkspaceId == workspaceId &&
                membership.Role == WorkspaceRole.Owner,
            ct);
        return workspaceHasOwner ? null : invitation;
    }

    private static ResendWorkspaceInvitationResponse ToResponse(
        WorkspaceInvitation invitation) =>
        new(
            invitation.Id,
            invitation.WorkspaceId,
            invitation.Email,
            invitation.Role,
            WorkspaceInvitationService.GetStatus(invitation),
            invitation.CreatedAt,
            invitation.ExpiresAt,
            invitation.LastSentAt,
            invitation.AcceptedAt,
            invitation.RevokedAt);
}

public sealed record ResendWorkspaceInvitationResponse(
    Guid Id,
    Guid WorkspaceId,
    string Email,
    WorkspaceRole Role,
    InvitationStatus Status,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    DateTime? LastSentAt,
    DateTime? AcceptedAt,
    DateTime? RevokedAt);
