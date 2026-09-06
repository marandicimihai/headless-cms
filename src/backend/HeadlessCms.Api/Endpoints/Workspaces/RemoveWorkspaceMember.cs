using FastEndpoints;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Endpoints.Workspaces;

public sealed class RemoveWorkspaceMember(
    ApplicationDbContext db,
    WorkspaceAccessService workspaceAccess)
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
        if (await workspaceAccess.ResolveAsync(
                User,
                request.WorkspaceId,
                WorkspaceAccessRoles.Owners,
                ct) is null)
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
}

public sealed class RemoveWorkspaceMemberRequest
{
    public Guid WorkspaceId { get; init; }
    public string UserId { get; init; } = default!;
}
