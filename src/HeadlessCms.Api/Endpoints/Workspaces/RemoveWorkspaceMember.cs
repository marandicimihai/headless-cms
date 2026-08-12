using FastEndpoints;
using FastEndpoints.Security;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Workspaces.Models;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Endpoints.Workspaces;

public sealed class RemoveWorkspaceMember(ApplicationDbContext db)
    : Endpoint<RemoveWorkspaceMemberRequest>
{
    public override void Configure()
    {
        Delete("workspaces/{workspaceId:guid}/members/{userId}");
        Claims("sub");
    }

    public override async Task HandleAsync(
        RemoveWorkspaceMemberRequest request,
        CancellationToken ct)
    {
        var ownerId = User.ClaimValue("sub")!;
        if (!await IsOwnerAsync(ownerId, request.WorkspaceId, ct))
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var membership = await db.WorkspaceMemberships.SingleOrDefaultAsync(
            item =>
                item.WorkspaceId == request.WorkspaceId &&
                item.UserId == request.UserId &&
                item.Role != WorkspaceRole.Owner,
            ct);
        if (membership is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        db.WorkspaceMemberships.Remove(membership);
        await db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }

    private Task<bool> IsOwnerAsync(string userId, Guid workspaceId, CancellationToken ct) =>
        db.WorkspaceMemberships.AnyAsync(
            membership =>
                membership.WorkspaceId == workspaceId &&
                membership.UserId == userId &&
                membership.Role == WorkspaceRole.Owner,
            ct);
}

public sealed class RemoveWorkspaceMemberRequest
{
    public Guid WorkspaceId { get; init; }
    public string UserId { get; init; } = default!;
}
