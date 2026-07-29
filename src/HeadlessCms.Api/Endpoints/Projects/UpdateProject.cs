using FastEndpoints;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Tenancy.Models;
using HeadlessCms.Api.Tenancy.Services;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Endpoints.Projects;

public sealed class UpdateProject(
    ApplicationDbContext db,
    TenantAccessService tenantAccess)
    : Endpoint<UpdateProjectRequest, ProjectResponse>
{
    public override void Configure()
    {
        Put("tenants/{tenantId:guid}/projects/{id:guid}");
        Claims("sub");
    }

    public override async Task HandleAsync(UpdateProjectRequest request, CancellationToken ct)
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

        var project = await db.Projects.SingleOrDefaultAsync(
            candidate =>
                candidate.Id == request.Id &&
                candidate.TenantId == request.TenantId,
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
