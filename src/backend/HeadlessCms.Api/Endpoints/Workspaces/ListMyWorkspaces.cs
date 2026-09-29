using FastEndpoints;
using FastEndpoints.Security;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Workspaces.Models;
using Microsoft.EntityFrameworkCore;

using HeadlessCms.Api.Documentation;

namespace HeadlessCms.Api.Endpoints.Workspaces;

public sealed class ListMyWorkspaces(ApplicationDbContext db)
    : EndpointWithoutRequest<IReadOnlyList<ListMyWorkspacesItemResponse>>
{
    public override void Configure()
    {
        Get("me/workspaces");
        Claims("sub");
        Description(b => b.WithTags("Workspaces"), clearDefaults: true);
        Summary((EndpointSummary s) =>
        {
            s.Summary = "List my workspaces";
            s.Description = "Access: Authenticated user.\n\nReturns an unpaginated array of the caller's memberships, including each currentRole. Platform administrators see only their own memberships here.";
            s.Response<IReadOnlyList<ListMyWorkspacesItemResponse>>(200, "Success.");
            s.ResponseExamples[200] = new[] { ApiExamples.Workspace };
            s.Response<ApiProblem>(401, "Authentication is required, or the session has expired.", "application/problem+json");
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = User.ClaimValue("sub")!;
        Response = await db.WorkspaceMemberships
            .AsNoTracking()
            .Where(membership => membership.UserId == userId)
            .OrderBy(membership => membership.Workspace.Name)
            .Select(membership => new ListMyWorkspacesItemResponse(
                membership.WorkspaceId,
                membership.Workspace.Name,
                membership.Workspace.CreatedAt,
                membership.Role))
            .ToListAsync(ct);
    }
}

public sealed record ListMyWorkspacesItemResponse(
    Guid Id,
    string Name,
    DateTime CreatedAt,
    WorkspaceRole? CurrentRole);
