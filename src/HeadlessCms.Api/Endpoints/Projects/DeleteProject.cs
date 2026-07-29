using FastEndpoints;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Tenancy.Models;
using HeadlessCms.Api.Tenancy.Services;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Endpoints.Projects;

public sealed class DeleteProjectRequest
{
    public Guid TenantId { get; init; }
    public Guid Id { get; init; }
}

public sealed class DeleteProject(
    ApplicationDbContext db,
    TenantAccessService tenantAccess)
    : Endpoint<DeleteProjectRequest>
{
    public override void Configure()
    {
        Delete("tenants/{tenantId:guid}/projects/{id:guid}");
        Claims("sub");
    }

    public override async Task HandleAsync(DeleteProjectRequest request, CancellationToken ct)
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

        db.Projects.Remove(project);
        await db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}
