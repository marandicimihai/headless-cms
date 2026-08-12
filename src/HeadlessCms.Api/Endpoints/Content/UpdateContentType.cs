using System.Text.Json;
using FluentValidation;
using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Content.Services;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;

namespace HeadlessCms.Api.Endpoints.Content;

public sealed class UpdateContentType(
    ContentDefinitionService definitions,
    WorkspaceAccessService workspaceAccess)
    : Endpoint<UpdateContentTypeRequest, UpdateContentTypeResponse>
{
    private const string KeyPattern = "^[a-z][a-z0-9_]*$";

    private static readonly IReadOnlySet<WorkspaceRole> Writers =
        new HashSet<WorkspaceRole>([WorkspaceRole.Owner, WorkspaceRole.Editor]);

    public override void Configure()
    {
        Put(
            "workspaces/{workspaceId:guid}/projects/{projectId:guid}/" +
            "content-types/{contentTypeKey}");
        Claims("sub");
    }

    public override async Task HandleAsync(UpdateContentTypeRequest request, CancellationToken ct)
    {
        if (await workspaceAccess.ResolveAsync(User, request.WorkspaceId, Writers, ct) is null)
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        try
        {
            var definition = await definitions.UpdateAsync(
                request.WorkspaceId,
                request.ProjectId,
                request.ContentTypeKey,
                request.Name.Trim(),
                ToInputs(request.Fields),
                ct);

            if (definition is null)
            {
                await Send.NotFoundAsync(ct);
                return;
            }

            Response = ToResponse(definition);
        }
        catch (ContentValidationException exception)
        {
            foreach (var error in exception.Errors)
                AddError(error);
            await Send.ErrorsAsync(cancellation: ct);
        }
    }

    internal static bool TryParseFieldType(string value) =>
        Enum.TryParse<ContentFieldType>(value, true, out _);

    internal static string FieldKeyPattern => KeyPattern;

    private static IReadOnlyList<ContentFieldInput> ToInputs(
        IEnumerable<UpdateContentTypeFieldRequest> fields) =>
        fields.Select(field => new ContentFieldInput(
                field.Key,
                field.Name.Trim(),
                Enum.Parse<ContentFieldType>(field.Type, true),
                field.Required,
                field.Nullable,
                field.Settings))
            .ToList();

    private static UpdateContentTypeResponse ToResponse(ContentTypeDefinition definition) =>
        new()
        {
            Id = definition.ContentType.Id,
            ProjectId = definition.ContentType.ProjectId,
            Key = definition.ContentType.Key,
            Name = definition.ContentType.Name,
            Version = definition.Version.Version,
            CreatedAt = definition.ContentType.CreatedAt,
            UpdatedAt = definition.ContentType.UpdatedAt,
            Fields = definition.Version.Fields
                .OrderBy(field => field.Position)
                .Select(field => new UpdateContentTypeFieldResponse
                {
                    Key = field.Key,
                    Name = field.Name,
                    Type = field.Type.ToString().ToLowerInvariant(),
                    Required = field.Required,
                    Nullable = field.Nullable,
                    Position = field.Position,
                    Settings = field.Settings.Clone()
                })
                .ToList()
        };
}

public sealed class UpdateContentTypeRequest
{
    public Guid WorkspaceId { get; init; }
    public Guid ProjectId { get; init; }
    public string ContentTypeKey { get; init; } = default!;
    public required string Name { get; init; }
    public required List<UpdateContentTypeFieldRequest> Fields { get; init; }
}

public sealed class UpdateContentTypeFieldRequest
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required string Type { get; init; }
    public bool Required { get; init; }
    public bool Nullable { get; init; }
    public JsonElement Settings { get; init; }
}

public sealed class UpdateContentTypeResponse
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public required string Key { get; init; }
    public required string Name { get; init; }
    public int Version { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public required IReadOnlyList<UpdateContentTypeFieldResponse> Fields { get; init; }
}

public sealed class UpdateContentTypeFieldResponse
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required string Type { get; init; }
    public bool Required { get; init; }
    public bool Nullable { get; init; }
    public int Position { get; init; }
    public JsonElement Settings { get; init; }
}

public sealed class UpdateContentTypeRequestValidator : Validator<UpdateContentTypeRequest>
{
    public UpdateContentTypeRequestValidator()
    {
        RuleFor(request => request.ContentTypeKey)
            .NotEmpty()
            .MaximumLength(64)
            .Matches(UpdateContentType.FieldKeyPattern);
        RuleFor(request => request.Name)
            .NotEmpty()
            .Must(name => name.Trim().Length is >= 3 and <= 100)
            .WithMessage("Name must contain between 3 and 100 characters.");
        RuleFor(request => request.Fields).NotEmpty();
        RuleForEach(request => request.Fields)
            .SetValidator(new UpdateContentTypeFieldRequestValidator());
    }
}

public sealed class UpdateContentTypeFieldRequestValidator
    : Validator<UpdateContentTypeFieldRequest>
{
    public UpdateContentTypeFieldRequestValidator()
    {
        RuleFor(request => request.Key)
            .NotEmpty()
            .MaximumLength(64)
            .Matches(UpdateContentType.FieldKeyPattern);
        RuleFor(request => request.Name)
            .NotEmpty()
            .Must(name => name.Trim().Length is >= 1 and <= 100)
            .WithMessage("Field name must contain between 1 and 100 characters.");
        RuleFor(request => request.Type)
            .Must(UpdateContentType.TryParseFieldType)
            .WithMessage("Type must be text, number, or boolean.");
        RuleFor(request => request.Settings)
            .Must(settings =>
                settings.ValueKind is JsonValueKind.Undefined or
                    JsonValueKind.Null or
                    JsonValueKind.Object)
            .WithMessage("Settings must be a JSON object.");
    }
}
