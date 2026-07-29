using FastEndpoints;
using FastEndpoints.Security;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Tenancy.Models;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Endpoints.Tenants;

public sealed class LeaveTenant(ApplicationDbContext db)
    : Endpoint<LeaveTenantRequest>
{
    public override void Configure()
    {
        Delete("me/tenants/{tenantId:guid}");
        Claims("sub");
    }

    public override async Task HandleAsync(LeaveTenantRequest request, CancellationToken ct)
    {
        var userId = User.ClaimValue("sub")!;
        var membership = await db.TenantMemberships.SingleOrDefaultAsync(
            candidate =>
                candidate.TenantId == request.TenantId &&
                candidate.UserId == userId,
            ct);

        if (membership is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (membership.Role == TenantRole.Owner)
        {
            await ApiErrors.SendAsync(
                HttpContext,
                StatusCodes.Status409Conflict,
                "owner_must_transfer",
                "Transfer ownership before leaving the tenant.",
                ct);
            return;
        }

        db.TenantMemberships.Remove(membership);
        await db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

public sealed class LeaveTenantRequest
{
    public Guid TenantId { get; init; }
}
