using FastEndpoints;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Tenancy.Models;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Endpoints.Tenants;

public sealed class ListTenants(ApplicationDbContext db)
    : Endpoint<ListTenantsRequest, ListTenantsResponse>
{
    public override void Configure()
    {
        Get("tenants");
        Roles(nameof(PlatformRole.PlatformAdmin));
    }

    public override async Task HandleAsync(ListTenantsRequest request, CancellationToken ct)
    {
        var (page, size) = NormalizePage(request.Page, request.PageSize);
        var query = db.Tenants.AsNoTracking().OrderBy(tenant => tenant.Name);
        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * size)
            .Take(size)
            .Select(tenant => new ListTenantsItemResponse(
                tenant.Id,
                tenant.Name,
                tenant.CreatedAt,
                null))
            .ToListAsync(ct);

        Response = new ListTenantsResponse(items, page, size, total);
    }

    private static (int Page, int Size) NormalizePage(int page, int size) =>
        (Math.Max(page, 1), Math.Clamp(size, 1, 100));
}

public sealed class ListTenantsRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed record ListTenantsItemResponse(
    Guid Id,
    string Name,
    DateTime CreatedAt,
    TenantRole? CurrentRole);

public sealed record ListTenantsResponse(
    IReadOnlyList<ListTenantsItemResponse> Items,
    int Page,
    int PageSize,
    int Total);
