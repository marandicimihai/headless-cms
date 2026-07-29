using FastEndpoints;
using FastEndpoints.Security;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Models;

namespace HeadlessCms.Api.Endpoints.Projects;

public sealed class CreateProject(ApplicationDbContext db)
    : Endpoint<CreateProjectRequest, ProjectResponse>
{
    public override void Configure()
    {
        Post("projects");
        Claims("sub");
    }

    public override async Task HandleAsync(CreateProjectRequest request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var project = new Project
        {
            Name = request.Name.Trim(),
            OwnerId = User.ClaimValue("sub")!,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Projects.Add(project);
        await db.SaveChangesAsync(ct);

        await Send.CreatedAtAsync<GetProject>(
            new { project.Id },
            project.ToResponse(),
            cancellation: ct);
    }
}
