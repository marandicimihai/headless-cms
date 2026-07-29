using FastEndpoints;
using FastEndpoints.Security;
using HeadlessCms.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Endpoints.Projects;

public sealed class ListProjects(ApplicationDbContext db)
    : EndpointWithoutRequest<IReadOnlyList<ProjectResponse>>
{
    public override void Configure()
    {
        Get("projects");
        Claims("sub");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var ownerId = User.ClaimValue("sub")!;

        Response = await db.Projects
            .AsNoTracking()
            .Where(project => project.OwnerId == ownerId)
            .OrderBy(project => project.Name)
            .Select(project => new ProjectResponse
            {
                Id = project.Id,
                Name = project.Name,
                CreatedAt = project.CreatedAt,
                UpdatedAt = project.UpdatedAt
            })
            .ToListAsync(ct);
    }
}
