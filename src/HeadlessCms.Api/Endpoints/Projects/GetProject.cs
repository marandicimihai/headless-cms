using FastEndpoints;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Tenancy.Services;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Endpoints.Projects;

public sealed class GetProjectRequest
{
    public Guid TenantId { get; init; }
    public Guid Id { get; init; }
}

public sealed class GetProjectResponse
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public required string Name { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed class GetProject(
    ApplicationDbContext db,
    TenantAccessService tenantAccess)
    : Endpoint<GetProjectRequest, GetProjectResponse>
{
    public override void Configure()
    {
        Get("tenants/{tenantId:guid}/projects/{id:guid}");
        Claims("sub");
    }

    public override async Task HandleAsync(GetProjectRequest request, CancellationToken ct)
    {
        var membership = await tenantAccess.FindMembershipAsync(User, request.TenantId, ct);

        if (membership is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var project = await db.Projects
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.Id == request.Id &&
                    candidate.TenantId == request.TenantId,
                ct);

        if (project is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        Response = new GetProjectResponse
        {
            Id = project.Id,
            TenantId = project.TenantId,
            Name = project.Name,
            CreatedAt = project.CreatedAt,
            UpdatedAt = project.UpdatedAt
        };
    }
}
