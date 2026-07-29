using FastEndpoints;
using FastEndpoints.Security;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Tenancy.Models;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Endpoints.Tenants;

public sealed class ListMyTenants(ApplicationDbContext db)
    : EndpointWithoutRequest<IReadOnlyList<ListMyTenantsItemResponse>>
{
    public override void Configure()
    {
        Get("me/tenants");
        Claims("sub");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = User.ClaimValue("sub")!;
        Response = await db.TenantMemberships
            .AsNoTracking()
            .Where(membership => membership.UserId == userId)
            .OrderBy(membership => membership.Tenant.Name)
            .Select(membership => new ListMyTenantsItemResponse(
                membership.TenantId,
                membership.Tenant.Name,
                membership.Tenant.CreatedAt,
                membership.Role))
            .ToListAsync(ct);
    }
}

public sealed record ListMyTenantsItemResponse(
    Guid Id,
    string Name,
    DateTime CreatedAt,
    TenantRole? CurrentRole);
