using FastEndpoints;
using FastEndpoints.Security;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Tenancy.Models;
using HeadlessCms.Api.Tenancy.Services;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Endpoints.Tenants;

public sealed class ResendTenantInvitation(
    ApplicationDbContext db,
    TenantInvitationService invitations)
    : EndpointWithoutRequest<ResendTenantInvitationResponse>
{
    public override void Configure()
    {
        Post("tenants/{tenantId:guid}/invitations/{invitationId:guid}/resend");
        Claims("sub");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var tenantId = Route<Guid>("tenantId");
        var invitationId = Route<Guid>("invitationId");
        var invitation = await FindManageableAsync(tenantId, invitationId, ct);
        if (invitation is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        try
        {
            await invitations.ResendAsync(invitation, ct);
            Response = ToResponse(invitation);
        }
        catch (InvitationFlowException exception)
        {
            await ApiErrors.SendAsync(HttpContext, exception, ct);
        }
    }

    private async Task<TenantInvitation?> FindManageableAsync(
        Guid tenantId,
        Guid invitationId,
        CancellationToken ct)
    {
        var invitation = await db.TenantInvitations.SingleOrDefaultAsync(
            candidate =>
                candidate.Id == invitationId &&
                candidate.TenantId == tenantId,
            ct);
        if (invitation is null)
            return null;

        var userId = User.ClaimValue("sub")!;
        var isOwner = await db.TenantMemberships.AnyAsync(
            membership =>
                membership.TenantId == tenantId &&
                membership.UserId == userId &&
                membership.Role == TenantRole.Owner,
            ct);
        if (isOwner)
            return invitation;

        if (!User.IsInRole(nameof(PlatformRole.PlatformAdmin)) ||
            invitation.Role != TenantRole.Owner)
        {
            return null;
        }

        var tenantHasOwner = await db.TenantMemberships.AnyAsync(
            membership =>
                membership.TenantId == tenantId &&
                membership.Role == TenantRole.Owner,
            ct);
        return tenantHasOwner ? null : invitation;
    }

    private static ResendTenantInvitationResponse ToResponse(
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

public sealed record ResendTenantInvitationResponse(
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
