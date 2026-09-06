using System.Security.Claims;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Workspaces.Models;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Workspaces.Services;

public sealed record WorkspaceAccessContext(
    Guid WorkspaceId,
    string UserId,
    WorkspaceRole Role);

public static class WorkspaceAccessRoles
{
    public static IReadOnlySet<WorkspaceRole> Members { get; } =
        new HashSet<WorkspaceRole>(
            [WorkspaceRole.Owner, WorkspaceRole.Editor, WorkspaceRole.Member]);

    public static IReadOnlySet<WorkspaceRole> Writers { get; } =
        new HashSet<WorkspaceRole>([WorkspaceRole.Owner, WorkspaceRole.Editor]);

    public static IReadOnlySet<WorkspaceRole> Owners { get; } =
        new HashSet<WorkspaceRole>([WorkspaceRole.Owner]);
}

public class WorkspaceAccessService(ApplicationDbContext db)
{
    public Task<WorkspaceAccessContext?> ResolveAsync(
        ClaimsPrincipal principal,
        Guid workspaceId,
        IReadOnlySet<WorkspaceRole> allowedRoles,
        CancellationToken ct = default)
    {
        var userId = principal.FindFirstValue("sub");

        if (userId is null)
            return Task.FromResult<WorkspaceAccessContext?>(null);

        return ResolveAsync(userId, workspaceId, allowedRoles, ct);
    }

    public async Task<WorkspaceAccessContext?> ResolveAsync(
        string userId,
        Guid workspaceId,
        IReadOnlySet<WorkspaceRole> allowedRoles,
        CancellationToken ct = default)
    {
        var membership = await db.WorkspaceMemberships
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.UserId == userId &&
                    candidate.WorkspaceId == workspaceId,
                ct);

        if (membership is null || !allowedRoles.Contains(membership.Role))
            return null;

        return new WorkspaceAccessContext(
            membership.WorkspaceId,
            membership.UserId,
            membership.Role);
    }
}
