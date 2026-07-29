using FastEndpoints;
using FastEndpoints.Security;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Tenancy.Models;
using HeadlessCms.Api.Tenancy.Services;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Endpoints.Tenants;

public sealed class RevokeTenantInvitation(
    ApplicationDbContext db,
    TenantInvitationService invitations)
    : Endpoint<RevokeTenantInvitationRequest>
{
    public override void Configure()
    {
        Delete("tenants/{tenantId:guid}/invitations/{invitationId:guid}");
        Claims("sub");
    }

    public override async Task HandleAsync(
        RevokeTenantInvitationRequest request,
        CancellationToken ct)
    {
        var invitation = await FindManageableAsync(request, ct);
        if (invitation is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        try
        {
            await invitations.RevokeAsync(invitation, ct);
            await Send.NoContentAsync(ct);
        }
        catch (InvitationFlowException exception)
        {
            await ApiErrors.SendAsync(HttpContext, exception, ct);
        }
    }

    private async Task<TenantInvitation?> FindManageableAsync(
        RevokeTenantInvitationRequest request,
        CancellationToken ct)
    {
        var invitation = await db.TenantInvitations.SingleOrDefaultAsync(
            candidate =>
                candidate.Id == request.InvitationId &&
                candidate.TenantId == request.TenantId,
            ct);
        if (invitation is null)
            return null;

        var userId = User.ClaimValue("sub")!;
        var isOwner = await db.TenantMemberships.AnyAsync(
            membership =>
                membership.TenantId == request.TenantId &&
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
                membership.TenantId == request.TenantId &&
                membership.Role == TenantRole.Owner,
            ct);
        return tenantHasOwner ? null : invitation;
    }
}

public sealed class RevokeTenantInvitationRequest
{
    public Guid TenantId { get; init; }
    public Guid InvitationId { get; init; }
}
