using FastEndpoints;
using FastEndpoints.Security;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Tenancy.Models;
using HeadlessCms.Api.Tenancy.Services;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Endpoints.Tenants;

public sealed class CreateTenantInvitation(
    TenantInvitationService invitations,
    ApplicationDbContext db)
    : Endpoint<CreateInvitationRequest, InvitationResponse>
{
    public override void Configure()
    {
        Post("tenants/{tenantId:guid}/invitations");
        Claims("sub");
    }

    public override async Task HandleAsync(CreateInvitationRequest request, CancellationToken ct)
    {
        try
        {
            var created = await invitations.CreateInvitationAsync(
                User.ClaimValue("sub")!,
                request.TenantId,
                request.Email,
                request.Role,
                ct);
            var invitation = await db.TenantInvitations
                .AsNoTracking()
                .SingleAsync(candidate => candidate.Id == created.InvitationId, ct);
            await Send.ResponseAsync(
                invitation.ToResponse(),
                StatusCodes.Status201Created,
                ct);
        }
        catch (InvitationFlowException exception)
        {
            await ApiErrors.SendAsync(HttpContext, exception, ct);
        }
    }
}

public sealed class ListTenantInvitations(ApplicationDbContext db)
    : Endpoint<InvitationPageRequest, PagedResponse<InvitationResponse>>
{
    public override void Configure()
    {
        Get("tenants/{tenantId:guid}/invitations");
        Claims("sub");
    }

    public override async Task HandleAsync(InvitationPageRequest request, CancellationToken ct)
    {
        var isAdmin = User.IsInRole(nameof(PlatformRole.PlatformAdmin));
        var userId = User.ClaimValue("sub")!;
        var isOwner = await db.TenantMemberships.AnyAsync(
            membership =>
                membership.TenantId == request.TenantId &&
                membership.UserId == userId &&
                membership.Role == TenantRole.Owner,
            ct);

        if (!isAdmin && !isOwner)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (isAdmin && !isOwner)
        {
            var tenantHasOwner = await db.TenantMemberships.AnyAsync(
                membership =>
                    membership.TenantId == request.TenantId &&
                    membership.Role == TenantRole.Owner,
                ct);
            if (tenantHasOwner)
            {
                await Send.NotFoundAsync(ct);
                return;
            }
        }

        var (page, size) = ListTenants.NormalizePage(request.Page, request.PageSize);
        var query = db.TenantInvitations
            .AsNoTracking()
            .Where(invitation => invitation.TenantId == request.TenantId);

        if (isAdmin && !isOwner)
            query = query.Where(invitation => invitation.Role == TenantRole.Owner);

        var all = await query.OrderByDescending(invitation => invitation.CreatedAt).ToListAsync(ct);
        if (request.Status is not null)
            all = all.Where(invitation =>
                    TenantInvitationService.GetStatus(invitation) == request.Status)
                .ToList();

        Response = new PagedResponse<InvitationResponse>(
            all.Skip((page - 1) * size).Take(size).Select(item => item.ToResponse()).ToList(),
            page,
            size,
            all.Count);
    }
}

public sealed class ResendTenantInvitation(
    ApplicationDbContext db,
    TenantInvitationService invitations)
    : EndpointWithoutRequest<InvitationResponse>
{
    public override void Configure()
    {
        Post("tenants/{tenantId:guid}/invitations/{invitationId:guid}/resend");
        Claims("sub");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var request = new InvitationResourceRequest
        {
            TenantId = Route<Guid>("tenantId"),
            InvitationId = Route<Guid>("invitationId")
        };
        var invitation = await FindManageableAsync(db, User, request, ct);
        if (invitation is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        try
        {
            await invitations.ResendAsync(invitation, ct);
            Response = invitation.ToResponse();
        }
        catch (InvitationFlowException exception)
        {
            await ApiErrors.SendAsync(HttpContext, exception, ct);
        }
    }

    internal static async Task<TenantInvitation?> FindManageableAsync(
        ApplicationDbContext db,
        System.Security.Claims.ClaimsPrincipal user,
        InvitationResourceRequest request,
        CancellationToken ct)
    {
        var invitation = await db.TenantInvitations.SingleOrDefaultAsync(
            candidate =>
                candidate.Id == request.InvitationId &&
                candidate.TenantId == request.TenantId,
            ct);
        if (invitation is null)
            return null;

        var userId = user.ClaimValue("sub")!;
        var isOwner = await db.TenantMemberships.AnyAsync(
            membership =>
                membership.TenantId == request.TenantId &&
                membership.UserId == userId &&
                membership.Role == TenantRole.Owner,
            ct);
        if (isOwner)
            return invitation;

        if (!user.IsInRole(nameof(PlatformRole.PlatformAdmin)) ||
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

public sealed class RevokeTenantInvitation(
    ApplicationDbContext db,
    TenantInvitationService invitations)
    : Endpoint<InvitationResourceRequest>
{
    public override void Configure()
    {
        Delete("tenants/{tenantId:guid}/invitations/{invitationId:guid}");
        Claims("sub");
    }

    public override async Task HandleAsync(InvitationResourceRequest request, CancellationToken ct)
    {
        var invitation =
            await ResendTenantInvitation.FindManageableAsync(db, User, request, ct);
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
}
