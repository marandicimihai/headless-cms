using System.Security.Claims;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Tenancy.Models;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Tenancy.Services;

public sealed record TenantAccessContext(
    Guid TenantId,
    string UserId,
    TenantRole Role);

public class TenantAccessService(ApplicationDbContext db)
{
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

    public async Task<TenantAccessContext?> ResolveAsync(
        ClaimsPrincipal principal,
        Guid tenantId,
        IReadOnlySet<TenantRole> allowedRoles,
        CancellationToken ct = default)
    {
        var membership = await FindMembershipAsync(principal, tenantId, ct);

        if (membership is null || !allowedRoles.Contains(membership.Role))
            return null;

        return new TenantAccessContext(
            membership.TenantId,
            membership.UserId,
            membership.Role);
    }
}
