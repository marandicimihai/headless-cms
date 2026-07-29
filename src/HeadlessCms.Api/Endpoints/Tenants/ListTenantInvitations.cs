using FastEndpoints;
using FastEndpoints.Security;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Tenancy.Models;
using HeadlessCms.Api.Tenancy.Services;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Endpoints.Tenants;

public sealed class ListTenantInvitations(ApplicationDbContext db)
    : Endpoint<ListTenantInvitationsRequest, ListTenantInvitationsResponse>
{
    public override void Configure()
    {
        Get("tenants/{tenantId:guid}/invitations");
        Claims("sub");
    }

    public override async Task HandleAsync(
        ListTenantInvitationsRequest request,
        CancellationToken ct)
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
            var tenantExists = await db.Tenants.AnyAsync(
                tenant => tenant.Id == request.TenantId,
                ct);
            if (!tenantExists)
            {
                await Send.NotFoundAsync(ct);
                return;
            }

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

        var (page, size) = NormalizePage(request.Page, request.PageSize);
        var query = db.TenantInvitations
            .AsNoTracking()
            .Where(invitation => invitation.TenantId == request.TenantId);

        if (isAdmin && !isOwner)
            query = query.Where(invitation => invitation.Role == TenantRole.Owner);

        var all = await query.OrderByDescending(invitation => invitation.CreatedAt).ToListAsync(ct);
        if (request.Status is not null)
        {
            all = all.Where(invitation =>
                    TenantInvitationService.GetStatus(invitation) == request.Status)
                .ToList();
        }

        Response = new ListTenantInvitationsResponse(
            all.Skip((page - 1) * size).Take(size).Select(ToResponse).ToList(),
            page,
            size,
            all.Count);
    }

    private static (int Page, int Size) NormalizePage(int page, int size) =>
        (Math.Max(page, 1), Math.Clamp(size, 1, 100));

    private static ListTenantInvitationsItemResponse ToResponse(
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

public sealed class ListTenantInvitationsRequest
{
    public Guid TenantId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public InvitationStatus? Status { get; init; }
}

public sealed record ListTenantInvitationsItemResponse(
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

public sealed record ListTenantInvitationsResponse(
    IReadOnlyList<ListTenantInvitationsItemResponse> Items,
    int Page,
    int PageSize,
    int Total);
