using FastEndpoints;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Endpoints.Workspaces;

public sealed class ChangeWorkspaceMemberRole(
    ApplicationDbContext db,
    WorkspaceAccessService workspaceAccess)
    : Endpoint<ChangeWorkspaceMemberRoleRequest, ChangeWorkspaceMemberRoleResponse>
{
    public override void Configure()
    {
        Patch("workspaces/{workspaceId:guid}/members/{userId}");
        Claims("sub");
    }

    public override async Task HandleAsync(
        ChangeWorkspaceMemberRoleRequest request,
        CancellationToken ct)
    {
        if (request.Role is not (WorkspaceRole.Editor or WorkspaceRole.Member))
        {
            await ApiErrors.SendAsync(
                HttpContext,
                StatusCodes.Status400BadRequest,
                "invalid_role",
                "Role must be Editor or Member.",
                ct);
            return;
        }

        if (await workspaceAccess.ResolveAsync(
                User,
                request.WorkspaceId,
                WorkspaceAccessRoles.Owners,
                ct) is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var membership = await db.WorkspaceMemberships
            .Include(item => item.User)
            .SingleOrDefaultAsync(
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

        membership.Role = request.Role;
        await db.SaveChangesAsync(ct);
        Response = new ChangeWorkspaceMemberRoleResponse(
            membership.UserId,
            membership.User.Email,
            membership.Role,
            membership.JoinedAt);
    }
}

public sealed class ChangeWorkspaceMemberRoleRequest
{
    public Guid WorkspaceId { get; init; }
    public string UserId { get; init; } = default!;
    public WorkspaceRole Role { get; init; }
}

public sealed record ChangeWorkspaceMemberRoleResponse(
    string UserId,
    string Email,
    WorkspaceRole Role,
    DateTime JoinedAt);
