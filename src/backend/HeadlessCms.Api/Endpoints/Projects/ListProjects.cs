using FastEndpoints;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Workspaces.Services;
using Microsoft.EntityFrameworkCore;

using HeadlessCms.Api.Documentation;

namespace HeadlessCms.Api.Endpoints.Projects;

public sealed class ListProjectsRequest
{
    public Guid WorkspaceId { get; init; }
}

public sealed class ListProjectsItemResponse
{
    public Guid Id { get; init; }
    public Guid WorkspaceId { get; init; }
    public required string Name { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed class ListProjects(
    ApplicationDbContext db,
    WorkspaceAccessService workspaceAccess)
    : Endpoint<ListProjectsRequest, IReadOnlyList<ListProjectsItemResponse>>
{
    public override void Configure()
    {
        Get("workspaces/{workspaceId:guid}/projects");
        Claims("sub");
        Description(b => b.WithTags("Projects"), clearDefaults: true);
        Summary((EndpointSummary s) =>
        {
            s.Summary = "List projects";
            s.Description = "Access: Workspace Owner, Editor, or Member.\n\nReturns an unpaginated array of projects in the workspace.";
            s.Params["WorkspaceId"] = "Workspace UUID.";
            s.Response<IReadOnlyList<ListProjectsItemResponse>>(200, "Success.");
            s.ResponseExamples[200] = new[] { ApiExamples.Project };
            s.Response<FastEndpoints.ProblemDetails>(400, "Invalid request or validation failure.", "application/problem+json");
            s.Response<ApiProblem>(401, "Authentication is required, or the session has expired.", "application/problem+json");
            s.Response(404, "Resource not found or inaccessible.");
        });
    }

    public override async Task HandleAsync(ListProjectsRequest request, CancellationToken ct)
    {
        if (await workspaceAccess.ResolveAsync(
                User,
                request.WorkspaceId,
                WorkspaceAccessRoles.Members,
                ct) is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        Response = await db.Projects
            .AsNoTracking()
            .Where(project => project.WorkspaceId == request.WorkspaceId)
            .OrderBy(project => project.Name)
            .Select(project => new ListProjectsItemResponse
            {
                Id = project.Id,
                WorkspaceId = project.WorkspaceId,
                Name = project.Name,
                CreatedAt = project.CreatedAt,
                UpdatedAt = project.UpdatedAt
            })
            .ToListAsync(ct);
    }
}
