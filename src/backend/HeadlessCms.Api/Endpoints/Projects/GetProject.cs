using FastEndpoints;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Workspaces.Services;
using Microsoft.EntityFrameworkCore;

using HeadlessCms.Api.Documentation;

namespace HeadlessCms.Api.Endpoints.Projects;

public sealed class GetProjectRequest
{
    public Guid WorkspaceId { get; init; }
    public Guid Id { get; init; }
}

public sealed class GetProjectResponse
{
    public Guid Id { get; init; }
    public Guid WorkspaceId { get; init; }
    public required string Name { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed class GetProject(
    ApplicationDbContext db,
    WorkspaceAccessService workspaceAccess)
    : Endpoint<GetProjectRequest, GetProjectResponse>
{
    public override void Configure()
    {
        Get("workspaces/{workspaceId:guid}/projects/{id:guid}");
        Claims("sub");
        Description(b => b.WithTags("Projects"), clearDefaults: true);
        Summary((EndpointSummary s) =>
        {
            s.Summary = "Get a project";
            s.Description = "Access: Workspace Owner, Editor, or Member.\n\nReturns a project belonging to the requested workspace. Missing or inaccessible resources return 404.";
            s.Params["WorkspaceId"] = "Workspace UUID.";
            s.Params["Id"] = "Project UUID within the workspace.";
            s.Response<GetProjectResponse>(200, "Success.");
            s.ResponseExamples[200] = ApiExamples.Project;
            s.Response<FastEndpoints.ProblemDetails>(400, "Invalid request or validation failure.", "application/problem+json");
            s.Response<ApiProblem>(401, "Authentication is required, or the session has expired.", "application/problem+json");
            s.Response(404, "Resource not found or inaccessible.");
        });
    }

    public override async Task HandleAsync(GetProjectRequest request, CancellationToken ct)
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

        var project = await db.Projects
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.Id == request.Id &&
                    candidate.WorkspaceId == request.WorkspaceId,
                ct);

        if (project is null)
            await Send.NotFoundAsync(ct);
        else
            Response = new GetProjectResponse
            {
                Id = project.Id,
                WorkspaceId = project.WorkspaceId,
                Name = project.Name,
                CreatedAt = project.CreatedAt,
                UpdatedAt = project.UpdatedAt
            };
    }
}
