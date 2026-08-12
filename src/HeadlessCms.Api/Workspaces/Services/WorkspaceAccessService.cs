using System.Security.Claims;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Workspaces.Models;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Workspaces.Services;

public sealed record WorkspaceAccessContext(
    Guid WorkspaceId,
    string UserId,
    WorkspaceRole Role);

public class WorkspaceAccessService(ApplicationDbContext db)
{
    public static bool CanReadContent(WorkspaceMembership membership) => true;

    public static bool CanWriteContent(WorkspaceMembership membership) =>
        membership.Role is WorkspaceRole.Owner or WorkspaceRole.Editor;

    public Task<WorkspaceMembership?> FindMembershipAsync(
        ClaimsPrincipal principal,
        Guid workspaceId,
        CancellationToken ct = default)
    {
        var userId = principal.FindFirstValue("sub");

        if (userId is null)
            return Task.FromResult<WorkspaceMembership?>(null);

        return db.WorkspaceMemberships
            .AsNoTracking()
            .SingleOrDefaultAsync(
                membership =>
                    membership.UserId == userId &&
                    membership.WorkspaceId == workspaceId,
                ct);
    }

    public async Task<WorkspaceAccessContext?> ResolveAsync(
        ClaimsPrincipal principal,
        Guid workspaceId,
        IReadOnlySet<WorkspaceRole> allowedRoles,
        CancellationToken ct = default)
    {
        var membership = await FindMembershipAsync(principal, workspaceId, ct);

        if (membership is null || !allowedRoles.Contains(membership.Role))
            return null;

        return new WorkspaceAccessContext(
            membership.WorkspaceId,
            membership.UserId,
            membership.Role);
    }
}
