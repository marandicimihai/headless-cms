using FastEndpoints;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Workspaces.Models;
using Microsoft.EntityFrameworkCore;

using HeadlessCms.Api.Documentation;

namespace HeadlessCms.Api.Endpoints.Workspaces;

public sealed class ListWorkspaces(ApplicationDbContext db)
    : Endpoint<ListWorkspacesRequest, ListWorkspacesResponse>
{
    public override void Configure()
    {
        Get("workspaces");
        Roles(nameof(PlatformRole.PlatformAdmin));
        Description(b => b.WithTags("Workspaces"), clearDefaults: true);
        Summary((EndpointSummary s) =>
        {
            s.Summary = "List all workspaces";
            s.Description = "Access: PlatformAdmin.\n\nLists all workspaces with pagination. currentRole is null. Page is clamped to at least 1 and pageSize to 1–100; defaults are 1 and 20.";
            s.Params["Page"] = "Page number, starting at 1 (default 1).";
            s.Params["PageSize"] = "Items per page (maximum 100). Default 20; values are clamped to 1–100.";
            s.Response<ListWorkspacesResponse>(200, "Success.");
            s.ResponseExamples[200] = ApiExamples.Page(ApiExamples.AdminWorkspace);
            s.Response<FastEndpoints.ProblemDetails>(400, "Invalid request or validation failure.", "application/problem+json");
            s.Response<ApiProblem>(401, "Authentication is required, or the session has expired.", "application/problem+json");
            s.Response<ApiProblem>(403, "The caller does not have permission.", "application/problem+json");
        });
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
    [System.ComponentModel.DefaultValue(1)]
    [FastEndpoints.QueryParam]
    public int Page { get; init; } = 1;
    [System.ComponentModel.DefaultValue(20)]
    [FastEndpoints.QueryParam]
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
