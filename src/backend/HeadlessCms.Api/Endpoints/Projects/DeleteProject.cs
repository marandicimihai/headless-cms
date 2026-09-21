using FastEndpoints;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;
using Microsoft.EntityFrameworkCore;

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
