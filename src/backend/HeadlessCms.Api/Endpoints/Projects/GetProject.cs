using HeadlessCms.Api.Caching;
using FastEndpoints;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Workspaces.Services;
using Microsoft.EntityFrameworkCore;

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
    WorkspaceAccessService workspaceAccess,
    ResourceCache cache)
    : Endpoint<GetProjectRequest, GetProjectResponse>
{
    public override void Configure()
    {
        Get("workspaces/{workspaceId:guid}/projects/{id:guid}");
        Claims("sub");
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

        var response = await cache.GetOrLoadAsync<GetProjectResponse>(
            request.WorkspaceId, ResourceCache.RequestKey(HttpContext.Request, nameof(GetProject)),
            async ct =>
            {
                var project = await db.Projects
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        candidate =>
                            candidate.Id == request.Id &&
                            candidate.WorkspaceId == request.WorkspaceId,
                        ct);

                if (project is null)
                {
                    return null;
                }

                return new GetProjectResponse
                {
                    Id = project.Id,
                    WorkspaceId = project.WorkspaceId,
                    Name = project.Name,
                    CreatedAt = project.CreatedAt,
                    UpdatedAt = project.UpdatedAt
                };
            }, ct);
        if (response is null)
            await Send.NotFoundAsync(ct);
        else
            Response = response;
    }
}
