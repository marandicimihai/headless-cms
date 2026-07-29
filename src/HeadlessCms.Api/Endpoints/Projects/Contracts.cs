using FluentValidation;

namespace HeadlessCms.Api.Endpoints.Projects;

public sealed class CreateProjectRequest
{
    public Guid TenantId { get; init; }
    public required string Name { get; init; }
}

public sealed class TenantProjectsRequest
{
    public Guid TenantId { get; init; }
}

public sealed class UpdateProjectRequest
{
    public Guid TenantId { get; init; }
    public Guid Id { get; init; }
    public required string Name { get; init; }
}

public sealed class ProjectIdRequest
{
    public Guid TenantId { get; init; }
    public Guid Id { get; init; }
}

public sealed class ProjectResponse
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

internal static class ProjectMappings
{
    public static ProjectResponse ToResponse(this Models.Project project) =>
        new()
        {
            Id = project.Id,
            TenantId = project.TenantId,
            Name = project.Name,
            CreatedAt = project.CreatedAt,
            UpdatedAt = project.UpdatedAt
        };
}
