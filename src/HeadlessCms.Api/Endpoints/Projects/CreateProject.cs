using FastEndpoints;
using FluentValidation;
using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;

namespace HeadlessCms.Api.Endpoints.Projects;

public sealed class CreateProjectRequest
{
    public Guid WorkspaceId { get; init; }
    public required string Name { get; init; }
}

public sealed class CreateProjectResponse
{
    public Guid Id { get; init; }
    public Guid WorkspaceId { get; init; }
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
    WorkspaceAccessService workspaceAccess)
    : Endpoint<CreateProjectRequest, CreateProjectResponse>
{
    public override void Configure()
    {
        Post("workspaces/{workspaceId:guid}/projects");
        Claims("sub");
    }

    public override async Task HandleAsync(CreateProjectRequest request, CancellationToken ct)
    {
        var membership = await workspaceAccess.FindMembershipAsync(User, request.WorkspaceId, ct);

        if (membership is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (membership.Role is not (WorkspaceRole.Owner or WorkspaceRole.Editor))
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        var now = DateTime.UtcNow;
        var project = new Project
        {
            Name = request.Name.Trim(),
            WorkspaceId = request.WorkspaceId,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Projects.Add(project);
        await db.SaveChangesAsync(ct);

        await Send.CreatedAtAsync<GetProject>(
            new { project.WorkspaceId, project.Id },
            ToResponse(project),
            cancellation: ct);
    }

    private static CreateProjectResponse ToResponse(Project project) =>
        new()
        {
            Id = project.Id,
            WorkspaceId = project.WorkspaceId,
            Name = project.Name,
            CreatedAt = project.CreatedAt,
            UpdatedAt = project.UpdatedAt
        };
}
