using FastEndpoints;
using FastEndpoints.Security;
using HeadlessCms.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Endpoints.Projects;

public sealed class DeleteProject(ApplicationDbContext db) : Endpoint<ProjectIdRequest>
{
    public override void Configure()
    {
        Delete("projects/{id:guid}");
        Claims("sub");
    }

    public override async Task HandleAsync(ProjectIdRequest request, CancellationToken ct)
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

        db.Projects.Remove(project);
        await db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}
