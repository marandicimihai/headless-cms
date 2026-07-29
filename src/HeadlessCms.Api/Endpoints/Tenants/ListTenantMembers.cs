using FastEndpoints;
using FastEndpoints.Security;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Tenancy.Models;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Endpoints.Tenants;

public sealed class ListTenantMembers(ApplicationDbContext db)
    : Endpoint<ListTenantMembersRequest, ListTenantMembersResponse>
{
    public override void Configure()
    {
        Get("tenants/{tenantId:guid}/members");
        Claims("sub");
    }

    public override async Task HandleAsync(
        ListTenantMembersRequest request,
        CancellationToken ct)
    {
        if (!await IsOwnerAsync(User.ClaimValue("sub")!, request.TenantId, ct))
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var (page, size) = NormalizePage(request.Page, request.PageSize);
        var query = db.TenantMemberships
            .AsNoTracking()
            .Where(membership => membership.TenantId == request.TenantId)
            .OrderBy(
                membership => membership.Role == TenantRole.Owner
                    ? 0
                    : membership.Role == TenantRole.Editor
                        ? 1
                        : 2)
            .ThenBy(membership => membership.User.Email);
        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * size)
            .Take(size)
            .Select(membership => new ListTenantMembersItemResponse(
                membership.UserId,
                membership.User.Email,
                membership.Role,
                membership.JoinedAt))
            .ToListAsync(ct);

        Response = new ListTenantMembersResponse(items, page, size, total);
    }

    private Task<bool> IsOwnerAsync(string userId, Guid tenantId, CancellationToken ct) =>
        db.TenantMemberships.AnyAsync(
            membership =>
                membership.TenantId == tenantId &&
                membership.UserId == userId &&
                membership.Role == TenantRole.Owner,
            ct);

    private static (int Page, int Size) NormalizePage(int page, int size) =>
        (Math.Max(page, 1), Math.Clamp(size, 1, 100));
}

public sealed class ListTenantMembersRequest
{
    public Guid TenantId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed record ListTenantMembersItemResponse(
    string UserId,
    string Email,
    TenantRole Role,
    DateTime JoinedAt);

public sealed record ListTenantMembersResponse(
    IReadOnlyList<ListTenantMembersItemResponse> Items,
    int Page,
    int PageSize,
    int Total);
