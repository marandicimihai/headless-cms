using FastEndpoints;
using FastEndpoints.Security;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Endpoints.Workspaces;

public sealed class RevokeWorkspaceInvitation(
    ApplicationDbContext db,
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
        var invitation = await FindManageableAsync(request, ct);
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

    private async Task<WorkspaceInvitation?> FindManageableAsync(
        RevokeWorkspaceInvitationRequest request,
        CancellationToken ct)
    {
        var invitation = await db.WorkspaceInvitations.SingleOrDefaultAsync(
            candidate =>
                candidate.Id == request.InvitationId &&
                candidate.WorkspaceId == request.WorkspaceId,
            ct);
        if (invitation is null)
            return null;

        var userId = User.ClaimValue("sub")!;
        var isOwner = await db.WorkspaceMemberships.AnyAsync(
            membership =>
                membership.WorkspaceId == request.WorkspaceId &&
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
                membership.WorkspaceId == request.WorkspaceId &&
                membership.Role == WorkspaceRole.Owner,
            ct);
        return workspaceHasOwner ? null : invitation;
    }
}

public sealed class RevokeWorkspaceInvitationRequest
{
    public Guid WorkspaceId { get; init; }
    public Guid InvitationId { get; init; }
}
