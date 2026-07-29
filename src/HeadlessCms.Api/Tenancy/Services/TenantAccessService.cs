using System.Security.Claims;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Tenancy.Models;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Tenancy.Services;

public class TenantAccessService(ApplicationDbContext db)
{
    public static bool CanReadContent(TenantMembership membership) => true;

    public static bool CanWriteContent(TenantMembership membership) =>
        membership.Role is TenantRole.Owner or TenantRole.Editor;

    public Task<TenantMembership?> FindMembershipAsync(
        ClaimsPrincipal principal,
        Guid tenantId,
        CancellationToken ct = default)
    {
        var userId = principal.FindFirstValue("sub");

        if (userId is null)
            return Task.FromResult<TenantMembership?>(null);

        return db.TenantMemberships
            .AsNoTracking()
            .SingleOrDefaultAsync(
                membership =>
                    membership.UserId == userId &&
                    membership.TenantId == tenantId,
                ct);
    }
}
