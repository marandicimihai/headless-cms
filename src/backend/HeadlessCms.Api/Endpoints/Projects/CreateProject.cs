using FastEndpoints;
using FluentValidation;
using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;

using HeadlessCms.Api.Documentation;

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
            .Must(name => name?.Trim().Length is >= 3 and <= 100)
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
        Description(b => b.WithTags("Projects"), clearDefaults: true);
        Summary((EndpointSummary s) =>
        {
            s.Summary = "Create a project";
            s.Description = "Access: Workspace Owner or Editor.\n\nCreates a project within the workspace. Name is trimmed and must contain 3–100 characters. Nonmembers receive 404; read-only members receive 403.";
            s.Params["WorkspaceId"] = "Workspace UUID.";
            s.Params["Name"] = "Display name; whitespace is trimmed. Must contain 3–100 characters after trimming.";
            s.ExampleRequest = new { Name = "Website" };
            s.Response<CreateProjectResponse>(201, "Created.");
            s.ResponseExamples[201] = ApiExamples.Project;
            s.Response<FastEndpoints.ProblemDetails>(400, "Invalid request or validation failure.", "application/problem+json");
            s.Response<ApiProblem>(401, "Authentication is required, or the session has expired.", "application/problem+json");
            s.Response(403, "The caller does not have permission.");
            s.Response(404, "Resource not found or inaccessible.");
        });
    }

    public override async Task HandleAsync(CreateProjectRequest request, CancellationToken ct)
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
