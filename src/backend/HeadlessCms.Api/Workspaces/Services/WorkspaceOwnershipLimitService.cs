using HeadlessCms.Api.Data;
using HeadlessCms.Api.Workspaces.Models;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Workspaces.Services;

public sealed class WorkspaceOwnershipLimitService(
    ApplicationDbContext db,
    IConfiguration configuration)
{
    public int MaximumOwnedWorkspaces
    {
        get
        {
            var maximum = configuration.GetValue<int?>("Workspaces:MaximumOwnedWorkspaces")
                          ?? throw new InvalidOperationException(
                              "Required configuration 'Workspaces:MaximumOwnedWorkspaces' is missing.");

            return maximum > 0
                ? maximum
                : throw new InvalidOperationException(
                    "Workspaces maximum owned workspaces must be greater than zero.");
        }
    }

    public async Task<bool> CanOwnAnotherWorkspaceAsync(
        string userId,
        CancellationToken ct = default)
    {
        var ownedWorkspaceCount = await db.WorkspaceMemberships.CountAsync(
            membership =>
                membership.UserId == userId &&
                membership.Role == WorkspaceRole.Owner,
            ct);

        return ownedWorkspaceCount < MaximumOwnedWorkspaces;
    }
}
