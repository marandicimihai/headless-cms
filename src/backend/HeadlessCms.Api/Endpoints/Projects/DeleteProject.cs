using FastEndpoints;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;
using Microsoft.EntityFrameworkCore;

using HeadlessCms.Api.Documentation;

namespace HeadlessCms.Api.Endpoints.Projects;

public sealed class DeleteProjectRequest
{
    public Guid WorkspaceId { get; init; }
    public Guid Id { get; init; }
}

public sealed class DeleteProject(
    ApplicationDbContext db,
    WorkspaceAccessService workspaceAccess)
    : Endpoint<DeleteProjectRequest>
{
    public override void Configure()
    {
        Delete("workspaces/{workspaceId:guid}/projects/{id:guid}");
        Claims("sub");
        Description(b => b.WithTags("Projects"), clearDefaults: true);
        Summary((EndpointSummary s) =>
        {
            s.Summary = "Delete a project";
            s.Description = "Access: Workspace Owner or Editor.\n\nPermanently deletes the project and its content types and entries.";
            s.Params["WorkspaceId"] = "Workspace UUID.";
            s.Params["Id"] = "Project UUID within the workspace.";
            s.Response(204, "No content.");
            s.Response<FastEndpoints.ProblemDetails>(400, "Invalid request or validation failure.", "application/problem+json");
            s.Response<ApiProblem>(401, "Authentication is required, or the session has expired.", "application/problem+json");
            s.Response(403, "The caller does not have permission.");
            s.Response(404, "Resource not found or inaccessible.");
        });
    }

    public override async Task HandleAsync(DeleteProjectRequest request, CancellationToken ct)
    {
        var access = await workspaceAccess.ResolveAsync(
            User,
            request.WorkspaceId,
            WorkspaceAccessRoles.Members,
            ct);

        if (access is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (!WorkspaceAccessRoles.Writers.Contains(access.Role))
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        var project = await db.Projects.SingleOrDefaultAsync(
            candidate =>
                candidate.Id == request.Id &&
                candidate.WorkspaceId == request.WorkspaceId,
            ct);

        if (project is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        db.Projects.Remove(project);
        await db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}
