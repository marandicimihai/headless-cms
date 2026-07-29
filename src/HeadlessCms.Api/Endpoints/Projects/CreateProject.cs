using FastEndpoints;
using FluentValidation;
using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Tenancy.Models;
using HeadlessCms.Api.Tenancy.Services;

namespace HeadlessCms.Api.Endpoints.Projects;

public sealed class CreateProjectRequest
{
    public Guid TenantId { get; init; }
    public required string Name { get; init; }
}

public sealed class CreateProjectResponse
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public required string Name { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed class CreateProjectRequestValidator : Validator<CreateProjectRequest>
{
    public CreateProjectRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .Must(name => name is not null && name.Trim().Length is >= 3 and <= 100)
            .WithMessage("Name must contain between 3 and 100 characters.");
    }
}

public sealed class CreateProject(
    ApplicationDbContext db,
    TenantAccessService tenantAccess)
    : Endpoint<CreateProjectRequest, CreateProjectResponse>
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
            ToResponse(project),
            cancellation: ct);
    }

    private static CreateProjectResponse ToResponse(Project project) =>
        new()
        {
            Id = project.Id,
            TenantId = project.TenantId,
            Name = project.Name,
            CreatedAt = project.CreatedAt,
            UpdatedAt = project.UpdatedAt
        };
}
