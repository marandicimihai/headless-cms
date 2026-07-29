using FastEndpoints;
using FastEndpoints.Security;
using HeadlessCms.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Endpoints.Projects;

public sealed class UpdateProject(ApplicationDbContext db)
    : Endpoint<UpdateProjectRequest, ProjectResponse>
{
    public override void Configure()
    {
        Put("projects/{id:guid}");
        Claims("sub");
    }

    public override async Task HandleAsync(UpdateProjectRequest request, CancellationToken ct)
    {
        var ownerId = User.ClaimValue("sub")!;
        var project = await db.Projects.SingleOrDefaultAsync(
            candidate => candidate.Id == request.Id && candidate.OwnerId == ownerId,
            ct);

        if (project is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        project.Name = request.Name.Trim();
        project.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        Response = project.ToResponse();
    }
}
