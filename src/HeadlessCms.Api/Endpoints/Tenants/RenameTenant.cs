using FastEndpoints;
using FluentValidation;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Tenancy.Models;
using HeadlessCms.Api.Tenancy.Services;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Endpoints.Tenants;

public sealed class RenameTenant(
    ApplicationDbContext db,
    TenantAccessService tenantAccess)
    : Endpoint<RenameTenantRequest, RenameTenantResponse>
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
        Response = new RenameTenantResponse(
            tenant.Id,
            tenant.Name,
            tenant.CreatedAt,
            currentRole);
    }
}

public sealed class RenameTenantRequest
{
    public Guid TenantId { get; init; }
    public required string Name { get; init; }
}

public sealed class RenameTenantRequestValidator : Validator<RenameTenantRequest>
{
    public RenameTenantRequestValidator() =>
        RuleFor(request => request.Name).NotEmpty().MaximumLength(100);
}

public sealed record RenameTenantResponse(
    Guid Id,
    string Name,
    DateTime CreatedAt,
    TenantRole? CurrentRole);
