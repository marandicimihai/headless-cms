using FastEndpoints;
using FastEndpoints.Security;
using HeadlessCms.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Endpoints.Projects;

public sealed class GetProject(ApplicationDbContext db)
    : Endpoint<ProjectIdRequest, ProjectResponse>
{
    public override void Configure()
    {
        Get("projects/{id:guid}");
        Claims("sub");
    }

    public override async Task HandleAsync(ProjectIdRequest request, CancellationToken ct)
    {
        var ownerId = User.ClaimValue("sub")!;
        var project = await db.Projects
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.Id == request.Id && candidate.OwnerId == ownerId,
                ct);

        if (project is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        Response = project.ToResponse();
    }
}
