using FastEndpoints;
using FastEndpoints.Security;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Workspaces.Models;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Endpoints.Workspaces;

public sealed class ListWorkspaceMembers(ApplicationDbContext db)
    : Endpoint<ListWorkspaceMembersRequest, ListWorkspaceMembersResponse>
{
    public override void Configure()
    {
        Get("workspaces/{workspaceId:guid}/members");
        Claims("sub");
    }

    public override async Task HandleAsync(
        ListWorkspaceMembersRequest request,
        CancellationToken ct)
    {
        if (!await IsOwnerAsync(User.ClaimValue("sub")!, request.WorkspaceId, ct))
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var (page, size) = NormalizePage(request.Page, request.PageSize);
        var query = db.WorkspaceMemberships
            .AsNoTracking()
            .Where(membership => membership.WorkspaceId == request.WorkspaceId)
            .OrderBy(
                membership => membership.Role == WorkspaceRole.Owner
                    ? 0
                    : membership.Role == WorkspaceRole.Editor
                        ? 1
                        : 2)
            .ThenBy(membership => membership.User.Email);
        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * size)
            .Take(size)
            .Select(membership => new ListWorkspaceMembersItemResponse(
                membership.UserId,
                membership.User.Email,
                membership.Role,
                membership.JoinedAt))
            .ToListAsync(ct);

        Response = new ListWorkspaceMembersResponse(items, page, size, total);
    }

    private Task<bool> IsOwnerAsync(string userId, Guid workspaceId, CancellationToken ct) =>
        db.WorkspaceMemberships.AnyAsync(
            membership =>
                membership.WorkspaceId == workspaceId &&
                membership.UserId == userId &&
                membership.Role == WorkspaceRole.Owner,
            ct);

    private static (int Page, int Size) NormalizePage(int page, int size) =>
        (Math.Max(page, 1), Math.Clamp(size, 1, 100));
}

public sealed class ListWorkspaceMembersRequest
{
    public Guid WorkspaceId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed record ListWorkspaceMembersItemResponse(
    string UserId,
    string Email,
    WorkspaceRole Role,
    DateTime JoinedAt);

public sealed record ListWorkspaceMembersResponse(
    IReadOnlyList<ListWorkspaceMembersItemResponse> Items,
    int Page,
    int PageSize,
    int Total);
