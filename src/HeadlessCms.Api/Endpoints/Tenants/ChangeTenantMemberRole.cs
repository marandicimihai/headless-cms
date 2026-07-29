using FastEndpoints;
using FastEndpoints.Security;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Tenancy.Models;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Endpoints.Tenants;

public sealed class ChangeTenantMemberRole(ApplicationDbContext db)
    : Endpoint<ChangeTenantMemberRoleRequest, ChangeTenantMemberRoleResponse>
{
    public override void Configure()
    {
        Patch("tenants/{tenantId:guid}/members/{userId}");
        Claims("sub");
    }

    public override async Task HandleAsync(
        ChangeTenantMemberRoleRequest request,
        CancellationToken ct)
    {
        if (request.Role is not (TenantRole.Editor or TenantRole.Member))
        {
            await ApiErrors.SendAsync(
                HttpContext,
                StatusCodes.Status400BadRequest,
                "invalid_role",
                "Role must be Editor or Member.",
                ct);
            return;
        }

        var ownerId = User.ClaimValue("sub")!;
        if (!await IsOwnerAsync(ownerId, request.TenantId, ct))
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var membership = await db.TenantMemberships
            .Include(item => item.User)
            .SingleOrDefaultAsync(
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

        membership.Role = request.Role;
        await db.SaveChangesAsync(ct);
        Response = new ChangeTenantMemberRoleResponse(
            membership.UserId,
            membership.User.Email,
            membership.Role,
            membership.JoinedAt);
    }

    private Task<bool> IsOwnerAsync(string userId, Guid tenantId, CancellationToken ct) =>
        db.TenantMemberships.AnyAsync(
            membership =>
                membership.TenantId == tenantId &&
                membership.UserId == userId &&
                membership.Role == TenantRole.Owner,
            ct);
}

public sealed class ChangeTenantMemberRoleRequest
{
    public Guid TenantId { get; init; }
    public string UserId { get; init; } = default!;
    public TenantRole Role { get; init; }
}

public sealed record ChangeTenantMemberRoleResponse(
    string UserId,
    string Email,
    TenantRole Role,
    DateTime JoinedAt);
