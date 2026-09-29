using FastEndpoints;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;
using Microsoft.EntityFrameworkCore;

using HeadlessCms.Api.Documentation;

namespace HeadlessCms.Api.Endpoints.Workspaces;

public sealed class ListWorkspaceMembers(
    ApplicationDbContext db,
    WorkspaceAccessService workspaceAccess)
    : Endpoint<ListWorkspaceMembersRequest, ListWorkspaceMembersResponse>
{
    public override void Configure()
    {
        Get("workspaces/{workspaceId:guid}/members");
        Claims("sub");
        Description(b => b.WithTags("Members"), clearDefaults: true);
        Summary((EndpointSummary s) =>
        {
            s.Summary = "List members";
            s.Description = "Access: Workspace Owner.\n\nPaginated by role then email. Page is clamped to at least 1 and pageSize to 1–100; defaults are 1 and 20.";
            s.Params["WorkspaceId"] = "Workspace UUID.";
            s.Params["Page"] = "Page number, starting at 1 (default 1).";
            s.Params["PageSize"] = "Items per page (maximum 100). Default 20; values are clamped to 1–100.";
            s.Response<ListWorkspaceMembersResponse>(200, "Success.");
            s.ResponseExamples[200] = ApiExamples.Page(ApiExamples.Member);
            s.Response<FastEndpoints.ProblemDetails>(400, "Invalid request or validation failure.", "application/problem+json");
            s.Response<ApiProblem>(401, "Authentication is required, or the session has expired.", "application/problem+json");
            s.Response(404, "Resource not found or inaccessible.");
        });
    }

    public override async Task HandleAsync(
        ListWorkspaceMembersRequest request,
        CancellationToken ct)
    {
        if (await workspaceAccess.ResolveAsync(
                User,
                request.WorkspaceId,
                WorkspaceAccessRoles.Owners,
                ct) is null)
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

    private static (int Page, int Size) NormalizePage(int page, int size) =>
        (Math.Max(page, 1), Math.Clamp(size, 1, 100));
}

public sealed class ListWorkspaceMembersRequest
{
    public Guid WorkspaceId { get; init; }
    [System.ComponentModel.DefaultValue(1)]
    [FastEndpoints.QueryParam]
    public int Page { get; init; } = 1;
    [System.ComponentModel.DefaultValue(20)]
    [FastEndpoints.QueryParam]
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
