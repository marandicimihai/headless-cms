using FastEndpoints;
using FastEndpoints.Security;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Workspaces.Models;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Endpoints.Workspaces;

public sealed class ListMyWorkspaces(ApplicationDbContext db)
    : EndpointWithoutRequest<IReadOnlyList<ListMyWorkspacesItemResponse>>
{
    public override void Configure()
    {
        Get("me/workspaces");
        Claims("sub");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = User.ClaimValue("sub")!;
        Response = await db.WorkspaceMemberships
            .AsNoTracking()
            .Where(membership => membership.UserId == userId)
            .OrderBy(membership => membership.Workspace.Name)
            .Select(membership => new ListMyWorkspacesItemResponse(
                membership.WorkspaceId,
                membership.Workspace.Name,
                membership.Workspace.CreatedAt,
                membership.Role))
            .ToListAsync(ct);
    }
}

public sealed record ListMyWorkspacesItemResponse(
    Guid Id,
    string Name,
    DateTime CreatedAt,
    WorkspaceRole? CurrentRole);
