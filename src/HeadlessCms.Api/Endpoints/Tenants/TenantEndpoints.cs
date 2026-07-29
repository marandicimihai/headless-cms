using FastEndpoints;
using FastEndpoints.Security;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Tenancy.Models;
using HeadlessCms.Api.Tenancy.Services;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Endpoints.Tenants;

public sealed class CreateTenant(
    TenantInvitationService invitations,
    ApplicationDbContext db)
    : Endpoint<CreateTenantRequest, CreateTenantResponse>
{
    public override void Configure()
    {
        Post("tenants");
        Roles(nameof(PlatformRole.PlatformAdmin));
    }

    public override async Task HandleAsync(CreateTenantRequest request, CancellationToken ct)
    {
        try
        {
            var created = await invitations.CreateTenantWithOwnerInvitationAsync(
                User.ClaimValue("sub")!,
                request.Name,
                request.OwnerEmail,
                ct);
            var tenantEntity = await db.Tenants
                .AsNoTracking()
                .SingleAsync(tenant => tenant.Id == created.TenantId, ct);
            var invitationEntity = await db.TenantInvitations
                .AsNoTracking()
                .SingleAsync(invitation => invitation.Id == created.InvitationId, ct);
            var tenant = new TenantResponse(
                tenantEntity.Id,
                tenantEntity.Name,
                tenantEntity.CreatedAt);

            await Send.ResponseAsync(
                new CreateTenantResponse(tenant, invitationEntity.ToResponse()),
                StatusCodes.Status201Created,
                ct);
        }
        catch (InvitationFlowException exception)
        {
            await ApiErrors.SendAsync(HttpContext, exception, ct);
        }
    }
}

public sealed class ListTenants(ApplicationDbContext db)
    : Endpoint<TenantPageRequest, PagedResponse<TenantResponse>>
{
    public override void Configure()
    {
        Get("tenants");
        Roles(nameof(PlatformRole.PlatformAdmin));
    }

    public override async Task HandleAsync(TenantPageRequest request, CancellationToken ct)
    {
        var (page, size) = NormalizePage(request.Page, request.PageSize);
        var query = db.Tenants.AsNoTracking().OrderBy(tenant => tenant.Name);
        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * size)
            .Take(size)
            .Select(tenant => new TenantResponse(
                tenant.Id,
                tenant.Name,
                tenant.CreatedAt,
                null))
            .ToListAsync(ct);

        Response = new PagedResponse<TenantResponse>(items, page, size, total);
    }

    internal static (int Page, int Size) NormalizePage(int page, int size) =>
        (Math.Max(page, 1), Math.Clamp(size, 1, 100));
}

public sealed class GetTenant(
    ApplicationDbContext db,
    TenantAccessService tenantAccess)
    : Endpoint<TenantResourceRequest, TenantResponse>
{
    public override void Configure()
    {
        Get("tenants/{tenantId:guid}");
        Claims("sub");
    }

    public override async Task HandleAsync(TenantResourceRequest request, CancellationToken ct)
    {
        TenantRole? role = null;
        if (!User.IsInRole(nameof(PlatformRole.PlatformAdmin)))
        {
            var membership = await tenantAccess.FindMembershipAsync(User, request.TenantId, ct);
            if (membership is null)
            {
                await Send.NotFoundAsync(ct);
                return;
            }

            role = membership.Role;
        }

        var response = await db.Tenants
            .AsNoTracking()
            .Where(tenant => tenant.Id == request.TenantId)
            .Select(tenant => new TenantResponse(
                tenant.Id,
                tenant.Name,
                tenant.CreatedAt,
                role))
            .SingleOrDefaultAsync(ct);

        if (response is null)
            await Send.NotFoundAsync(ct);
        else
            Response = response;
    }
}

public sealed class RenameTenant(
    ApplicationDbContext db,
    TenantAccessService tenantAccess)
    : Endpoint<RenameTenantRequest, TenantResponse>
{
    public override void Configure()
    {
        Patch("tenants/{tenantId:guid}");
        Claims("sub");
    }

    public override async Task HandleAsync(RenameTenantRequest request, CancellationToken ct)
    {
        TenantRole? currentRole = null;
        if (!User.IsInRole(nameof(PlatformRole.PlatformAdmin)))
        {
            var membership = await tenantAccess.FindMembershipAsync(User, request.TenantId, ct);
            if (membership?.Role != TenantRole.Owner)
            {
                await Send.NotFoundAsync(ct);
                return;
            }

            currentRole = membership.Role;
        }

        var tenant = await db.Tenants.SingleOrDefaultAsync(
            candidate => candidate.Id == request.TenantId,
            ct);
        if (tenant is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        tenant.Name = request.Name.Trim();
        await db.SaveChangesAsync(ct);
        Response = new TenantResponse(tenant.Id, tenant.Name, tenant.CreatedAt, currentRole);
    }
}

public sealed class ListMyTenants(ApplicationDbContext db)
    : EndpointWithoutRequest<IReadOnlyList<TenantResponse>>
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
            .Select(membership => new TenantResponse(
                membership.TenantId,
                membership.Tenant.Name,
                membership.Tenant.CreatedAt,
                membership.Role))
            .ToListAsync(ct);
    }
}

public sealed class LeaveTenant(ApplicationDbContext db)
    : Endpoint<TenantResourceRequest>
{
    public override void Configure()
    {
        Delete("me/tenants/{tenantId:guid}");
        Claims("sub");
    }

    public override async Task HandleAsync(TenantResourceRequest request, CancellationToken ct)
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
