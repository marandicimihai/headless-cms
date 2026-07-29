using FastEndpoints;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Tenancy.Services;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Endpoints.Projects;

public sealed class ListProjects(
    ApplicationDbContext db,
    TenantAccessService tenantAccess)
    : Endpoint<TenantProjectsRequest, IReadOnlyList<ProjectResponse>>
{
    public override void Configure()
    {
        Get("tenants/{tenantId:guid}/projects");
        Claims("sub");
    }

    public override async Task HandleAsync(TenantProjectsRequest request, CancellationToken ct)
    {
        var membership = await tenantAccess.FindMembershipAsync(User, request.TenantId, ct);

        if (membership is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        Response = await db.Projects
            .AsNoTracking()
            .Where(project => project.TenantId == request.TenantId)
            .OrderBy(project => project.Name)
            .Select(project => new ProjectResponse
            {
                Id = project.Id,
                TenantId = project.TenantId,
                Name = project.Name,
                CreatedAt = project.CreatedAt,
                UpdatedAt = project.UpdatedAt
            })
            .ToListAsync(ct);
    }
}
