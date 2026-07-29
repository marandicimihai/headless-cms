using FastEndpoints;
using FastEndpoints.Security;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Tenancy.Models;
using HeadlessCms.Api.Tenancy.Services;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Endpoints.Tenants;

public sealed class GetTenant(
    ApplicationDbContext db,
    TenantAccessService tenantAccess)
    : Endpoint<GetTenantRequest, GetTenantResponse>
{
    public override void Configure()
    {
        Get("tenants/{tenantId:guid}");
        Claims("sub");
    }

    public override async Task HandleAsync(GetTenantRequest request, CancellationToken ct)
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
            .Select(tenant => new GetTenantResponse(
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

public sealed class GetTenantRequest
{
    public Guid TenantId { get; init; }
}

public sealed record GetTenantResponse(
    Guid Id,
    string Name,
    DateTime CreatedAt,
    TenantRole? CurrentRole);
