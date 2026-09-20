using HeadlessCms.Api.Caching;
using FastEndpoints;
using FluentValidation;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Endpoints.Projects;

public sealed class UpdateProjectRequest
{
    public Guid WorkspaceId { get; init; }
    public Guid Id { get; init; }
    public required string Name { get; init; }
}

public sealed class UpdateProjectResponse
{
    public Guid Id { get; init; }
    public Guid WorkspaceId { get; init; }
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
    WorkspaceAccessService workspaceAccess,
    ResourceCache cache)
    : Endpoint<UpdateProjectRequest, UpdateProjectResponse>
{
    public override void Configure()
    {
        Put("workspaces/{workspaceId:guid}/projects/{id:guid}");
        Claims("sub");
    }

    public override async Task HandleAsync(UpdateProjectRequest request, CancellationToken ct)
    {
        var access = await workspaceAccess.ResolveAsync(
            User,
            request.WorkspaceId,
            WorkspaceAccessRoles.Members,
            ct);

        if (access is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (!WorkspaceAccessRoles.Writers.Contains(access.Role))
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        var project = await db.Projects.SingleOrDefaultAsync(
            candidate =>
                candidate.Id == request.Id &&
                candidate.WorkspaceId == request.WorkspaceId,
            ct);

        if (project is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        project.Name = request.Name.Trim();
        project.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await cache.InvalidateWorkspaceAsync(request.WorkspaceId);

        Response = new UpdateProjectResponse
        {
            Id = project.Id,
            WorkspaceId = project.WorkspaceId,
            Name = project.Name,
            CreatedAt = project.CreatedAt,
            UpdatedAt = project.UpdatedAt
        };
    }
}
