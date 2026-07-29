using FastEndpoints;
using FluentValidation;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Tenancy.Models;
using HeadlessCms.Api.Tenancy.Services;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Endpoints.Projects;

public sealed class UpdateProjectRequest
{
    public Guid TenantId { get; init; }
    public Guid Id { get; init; }
    public required string Name { get; init; }
}

public sealed class UpdateProjectResponse
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public required string Name { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed class UpdateProjectRequestValidator : Validator<UpdateProjectRequest>
{
    public UpdateProjectRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .Must(name => name is not null && name.Trim().Length is >= 3 and <= 100)
            .WithMessage("Name must contain between 3 and 100 characters.");
    }
}

public sealed class UpdateProject(
    ApplicationDbContext db,
    TenantAccessService tenantAccess)
    : Endpoint<UpdateProjectRequest, UpdateProjectResponse>
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

        Response = new UpdateProjectResponse
        {
            Id = project.Id,
            TenantId = project.TenantId,
            Name = project.Name,
            CreatedAt = project.CreatedAt,
            UpdatedAt = project.UpdatedAt
        };
    }
}
