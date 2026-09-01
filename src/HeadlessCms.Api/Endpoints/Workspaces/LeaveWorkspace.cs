using FastEndpoints;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Endpoints.Workspaces;

public sealed class LeaveWorkspace(
    ApplicationDbContext db,
    WorkspaceAccessService workspaceAccess)
    : Endpoint<LeaveWorkspaceRequest>
{
    public override void Configure()
    {
        Delete("me/workspaces/{workspaceId:guid}");
        Claims("sub");
    }

    public override async Task HandleAsync(LeaveWorkspaceRequest request, CancellationToken ct)
    {
        var access = await workspaceAccess.ResolveAsync(
            User,
            request.WorkspaceId,
            WorkspaceAccessRoles.Members,
            ct);
        if (access is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var membership = await db.WorkspaceMemberships.SingleOrDefaultAsync(
            candidate =>
                candidate.WorkspaceId == request.WorkspaceId &&
                candidate.UserId == access.UserId,
            ct);

        if (membership is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (membership.Role == WorkspaceRole.Owner)
        {
            await ApiErrors.SendAsync(
                HttpContext,
                StatusCodes.Status409Conflict,
                "owner_must_transfer",
                "Transfer ownership before leaving the workspace.",
                ct);
            return;
        }

        db.WorkspaceMemberships.Remove(membership);
        await db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

public sealed class LeaveWorkspaceRequest
{
    public Guid WorkspaceId { get; init; }
}
