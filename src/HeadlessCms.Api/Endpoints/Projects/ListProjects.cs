using FastEndpoints;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Workspaces.Services;
using Microsoft.EntityFrameworkCore;

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
    }

    public override async Task HandleAsync(ListProjectsRequest request, CancellationToken ct)
    {
        var membership = await workspaceAccess.FindMembershipAsync(User, request.WorkspaceId, ct);

        if (membership is null)
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
