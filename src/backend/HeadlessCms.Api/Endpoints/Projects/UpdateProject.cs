using FastEndpoints;
using FluentValidation;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;
using Microsoft.EntityFrameworkCore;

using HeadlessCms.Api.Documentation;

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
    WorkspaceAccessService workspaceAccess)
    : Endpoint<UpdateProjectRequest, UpdateProjectResponse>
{
    public override void Configure()
    {
        Put("workspaces/{workspaceId:guid}/projects/{id:guid}");
        Claims("sub");
        Description(b => b.WithTags("Projects"), clearDefaults: true);
        Summary((EndpointSummary s) =>
        {
            s.Summary = "Update a project";
            s.Description = "Access: Workspace Owner or Editor.\n\nReplaces the project name. Name is trimmed and must contain 3–100 characters.";
            s.Params["WorkspaceId"] = "Workspace UUID.";
            s.Params["Id"] = "Project UUID within the workspace.";
            s.Params["Name"] = "Display name; whitespace is trimmed. Must contain 3–100 characters after trimming.";
            s.ExampleRequest = new { Name = "Website" };
            s.Response<UpdateProjectResponse>(200, "Success.");
            s.ResponseExamples[200] = ApiExamples.Project;
            s.Response<FastEndpoints.ProblemDetails>(400, "Invalid request or validation failure.", "application/problem+json");
            s.Response<ApiProblem>(401, "Authentication is required, or the session has expired.", "application/problem+json");
            s.Response(403, "The caller does not have permission.");
            s.Response(404, "Resource not found or inaccessible.");
        });
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
