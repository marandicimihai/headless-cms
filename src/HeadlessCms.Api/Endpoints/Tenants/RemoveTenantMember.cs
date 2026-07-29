using FastEndpoints;
using FastEndpoints.Security;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Tenancy.Models;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Endpoints.Tenants;

public sealed class RemoveTenantMember(ApplicationDbContext db)
    : Endpoint<RemoveTenantMemberRequest>
{
    public override void Configure()
    {
        Delete("tenants/{tenantId:guid}/members/{userId}");
        Claims("sub");
    }

    public override async Task HandleAsync(
        RemoveTenantMemberRequest request,
        CancellationToken ct)
    {
        var ownerId = User.ClaimValue("sub")!;
        if (!await IsOwnerAsync(ownerId, request.TenantId, ct))
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var membership = await db.TenantMemberships.SingleOrDefaultAsync(
            item =>
                item.TenantId == request.TenantId &&
                item.UserId == request.UserId &&
                item.Role != TenantRole.Owner,
            ct);
        if (membership is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        db.TenantMemberships.Remove(membership);
        await db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }

    private Task<bool> IsOwnerAsync(string userId, Guid tenantId, CancellationToken ct) =>
        db.TenantMemberships.AnyAsync(
            membership =>
                membership.TenantId == tenantId &&
                membership.UserId == userId &&
                membership.Role == TenantRole.Owner,
            ct);
}

public sealed class RemoveTenantMemberRequest
{
    public Guid TenantId { get; init; }
    public string UserId { get; init; } = default!;
}
