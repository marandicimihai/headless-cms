using FastEndpoints;
using FastEndpoints.Security;
using FluentValidation;
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
            var tenant = new CreateTenantTenantResponse(
                tenantEntity.Id,
                tenantEntity.Name,
                tenantEntity.CreatedAt);

            await Send.ResponseAsync(
                new CreateTenantResponse(tenant, ToResponse(invitationEntity)),
                StatusCodes.Status201Created,
                ct);
        }
        catch (InvitationFlowException exception)
        {
            await ApiErrors.SendAsync(HttpContext, exception, ct);
        }
    }

    private static CreateTenantOwnerInvitationResponse ToResponse(
        TenantInvitation invitation) =>
        new(
            invitation.Id,
            invitation.TenantId,
            invitation.Email,
            invitation.Role,
            TenantInvitationService.GetStatus(invitation),
            invitation.CreatedAt,
            invitation.ExpiresAt,
            invitation.LastSentAt,
            invitation.AcceptedAt,
            invitation.RevokedAt);
}

public sealed class CreateTenantRequest
{
    public required string Name { get; init; }
    public required string OwnerEmail { get; init; }
}

public sealed class CreateTenantRequestValidator : Validator<CreateTenantRequest>
{
    public CreateTenantRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(100);
        RuleFor(request => request.OwnerEmail).NotEmpty().EmailAddress().MaximumLength(320);
    }
}

public sealed record CreateTenantTenantResponse(
    Guid Id,
    string Name,
    DateTime CreatedAt,
    TenantRole? CurrentRole = null);

public sealed record CreateTenantOwnerInvitationResponse(
    Guid Id,
    Guid TenantId,
    string Email,
    TenantRole Role,
    InvitationStatus Status,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    DateTime? LastSentAt,
    DateTime? AcceptedAt,
    DateTime? RevokedAt);

public sealed record CreateTenantResponse(
    CreateTenantTenantResponse Tenant,
    CreateTenantOwnerInvitationResponse OwnerInvitation);
