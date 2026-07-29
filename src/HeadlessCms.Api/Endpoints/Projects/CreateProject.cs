using FastEndpoints;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Models;
using HeadlessCms.Api.Tenancy.Models;
using HeadlessCms.Api.Tenancy.Services;

namespace HeadlessCms.Api.Endpoints.Projects;

public sealed class CreateProject(
    ApplicationDbContext db,
    TenantAccessService tenantAccess)
    : Endpoint<CreateProjectRequest, ProjectResponse>
{
    public override void Configure()
    {
        Post("tenants/{tenantId:guid}/projects");
        Claims("sub");
    }

    public override async Task HandleAsync(CreateProjectRequest request, CancellationToken ct)
    {
        var membership = await tenantAccess.FindMembershipAsync(User, request.TenantId, ct);

        if (membership is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (membership.Role is not (TenantRole.Owner or TenantRole.Editor))
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        var now = DateTime.UtcNow;
        var project = new Project
        {
            Name = request.Name.Trim(),
            TenantId = request.TenantId,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Projects.Add(project);
        await db.SaveChangesAsync(ct);

        await Send.CreatedAtAsync<GetProject>(
            new { project.TenantId, project.Id },
            project.ToResponse(),
            cancellation: ct);
    }
}
