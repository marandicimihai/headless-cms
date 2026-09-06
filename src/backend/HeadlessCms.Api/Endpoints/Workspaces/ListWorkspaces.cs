using FastEndpoints;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Workspaces.Models;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Endpoints.Workspaces;

public sealed class ListWorkspaces(ApplicationDbContext db)
    : Endpoint<ListWorkspacesRequest, ListWorkspacesResponse>
{
    public override void Configure()
    {
        Get("workspaces");
        Roles(nameof(PlatformRole.PlatformAdmin));
    }

    public override async Task HandleAsync(ListWorkspacesRequest request, CancellationToken ct)
    {
        var (page, size) = NormalizePage(request.Page, request.PageSize);
        var query = db.Workspaces.AsNoTracking().OrderBy(workspace => workspace.Name);
        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * size)
            .Take(size)
            .Select(workspace => new ListWorkspacesItemResponse(
                workspace.Id,
                workspace.Name,
                workspace.CreatedAt,
                null))
            .ToListAsync(ct);

        Response = new ListWorkspacesResponse(items, page, size, total);
    }

    private static (int Page, int Size) NormalizePage(int page, int size) =>
        (Math.Max(page, 1), Math.Clamp(size, 1, 100));
}

public sealed class ListWorkspacesRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed record ListWorkspacesItemResponse(
    Guid Id,
    string Name,
    DateTime CreatedAt,
    WorkspaceRole? CurrentRole);

public sealed record ListWorkspacesResponse(
    IReadOnlyList<ListWorkspacesItemResponse> Items,
    int Page,
    int PageSize,
    int Total);
