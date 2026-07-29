using FastEndpoints;
using FastEndpoints.Security;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Tenancy.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HeadlessCms.Api.Endpoints.Tenants;

public sealed class ListTenantMembers(ApplicationDbContext db)
    : Endpoint<MemberPageRequest, PagedResponse<MemberResponse>>
{
    public override void Configure()
    {
        Get("tenants/{tenantId:guid}/members");
        Claims("sub");
    }

    public override async Task HandleAsync(MemberPageRequest request, CancellationToken ct)
    {
        if (!await IsOwnerAsync(db, User.ClaimValue("sub")!, request.TenantId, ct))
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var (page, size) = ListTenants.NormalizePage(request.Page, request.PageSize);
        var query = db.TenantMemberships
            .AsNoTracking()
            .Where(membership => membership.TenantId == request.TenantId)
            .OrderByDescending(membership => membership.Role)
            .ThenBy(membership => membership.User.Email);
        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * size)
            .Take(size)
            .Select(membership => new MemberResponse(
                membership.UserId,
                membership.User.Email,
                membership.Role,
                membership.JoinedAt))
            .ToListAsync(ct);

        Response = new PagedResponse<MemberResponse>(items, page, size, total);
    }

    internal static Task<bool> IsOwnerAsync(
        ApplicationDbContext db,
        string userId,
        Guid tenantId,
        CancellationToken ct) =>
        db.TenantMemberships.AnyAsync(
            membership =>
                membership.TenantId == tenantId &&
                membership.UserId == userId &&
                membership.Role == TenantRole.Owner,
            ct);
}

public sealed class ChangeTenantMemberRole(ApplicationDbContext db)
    : Endpoint<ChangeMemberRoleRequest, MemberResponse>
{
    public override void Configure()
    {
        Patch("tenants/{tenantId:guid}/members/{userId}");
        Claims("sub");
    }

    public override async Task HandleAsync(ChangeMemberRoleRequest request, CancellationToken ct)
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
        if (!await ListTenantMembers.IsOwnerAsync(db, ownerId, request.TenantId, ct))
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
        Response = new MemberResponse(
            membership.UserId,
            membership.User.Email,
            membership.Role,
            membership.JoinedAt);
    }
}

public sealed class RemoveTenantMember(ApplicationDbContext db)
    : Endpoint<MemberResourceRequest>
{
    public override void Configure()
    {
        Delete("tenants/{tenantId:guid}/members/{userId}");
        Claims("sub");
    }

    public override async Task HandleAsync(MemberResourceRequest request, CancellationToken ct)
    {
        var ownerId = User.ClaimValue("sub")!;
        if (!await ListTenantMembers.IsOwnerAsync(db, ownerId, request.TenantId, ct))
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
}

public sealed class TransferTenantOwnership(ApplicationDbContext db)
    : Endpoint<TransferOwnershipRequest, IReadOnlyList<MemberResponse>>
{
    public override void Configure()
    {
        Post("tenants/{tenantId:guid}/ownership-transfer");
        Claims("sub");
    }

    public override async Task HandleAsync(TransferOwnershipRequest request, CancellationToken ct)
    {
        var ownerId = User.ClaimValue("sub")!;
        var owner = await db.TenantMemberships
            .Include(membership => membership.User)
            .SingleOrDefaultAsync(
                membership =>
                    membership.TenantId == request.TenantId &&
                    membership.UserId == ownerId &&
                    membership.Role == TenantRole.Owner,
                ct);
        var nextOwner = await db.TenantMemberships
            .Include(membership => membership.User)
            .SingleOrDefaultAsync(
                membership =>
                    membership.TenantId == request.TenantId &&
                    membership.UserId == request.NewOwnerUserId &&
                    membership.Role != TenantRole.Owner,
                ct);

        if (owner is null || nextOwner is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        IDbContextTransaction? transaction = null;
        if (db.Database.IsRelational())
            transaction = await db.Database.BeginTransactionAsync(ct);

        try
        {
            owner.Role = TenantRole.Editor;
            await db.SaveChangesAsync(ct);
            nextOwner.Role = TenantRole.Owner;
            await db.SaveChangesAsync(ct);
            if (transaction is not null)
                await transaction.CommitAsync(ct);
        }
        catch
        {
            if (transaction is not null)
                await transaction.RollbackAsync(ct);
            throw;
        }
        finally
        {
            if (transaction is not null)
                await transaction.DisposeAsync();
        }

        Response =
        [
            new MemberResponse(owner.UserId, owner.User.Email, owner.Role, owner.JoinedAt),
            new MemberResponse(
                nextOwner.UserId,
                nextOwner.User.Email,
                nextOwner.Role,
                nextOwner.JoinedAt)
        ];
    }
}
