using System.Text.Json;
using FluentValidation;
using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Content.Services;
using HeadlessCms.Api.Workspaces.Services;

namespace HeadlessCms.Api.Endpoints.Content;

public sealed class UpdateContentType(
    ContentDefinitionService definitions,
    WorkspaceAccessService workspaceAccess)
    : Endpoint<UpdateContentTypeRequest, UpdateContentTypeResponse>
{
    private const string KeyPattern = "^[a-z][a-z0-9_]*$";

    public override void Configure()
    {
        Put(
            "workspaces/{workspaceId:guid}/projects/{projectId:guid}/" +
            "content-types/{contentTypeKey}");
        Claims("sub");
    }

    public override async Task HandleAsync(UpdateContentTypeRequest request, CancellationToken ct)
    {
        if (await workspaceAccess.ResolveAsync(
                User,
                request.WorkspaceId,
                WorkspaceAccessRoles.Writers,
                ct) is null)
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

    internal static string FieldKeyPattern => KeyPattern;

    private static IReadOnlyList<ContentFieldInput> ToInputs(
        IEnumerable<UpdateContentTypeFieldRequest> fields) =>
        fields.Select(field => new ContentFieldInput(
                field.Key,
                field.Type,
                field.Required,
                field.Settings))
            .ToList();

    private static UpdateContentTypeResponse ToResponse(ContentTypeDefinition definition) =>
        new()
        {
            Id = definition.ContentType.Id,
            ProjectId = definition.ContentType.ProjectId,
            Key = definition.ContentType.Key,
            CreatedAt = definition.ContentType.CreatedAt,
            UpdatedAt = definition.ContentType.UpdatedAt,
            Fields = definition.ContentType.Fields
                .OrderBy(field => field.Position)
                .Select(field => new UpdateContentTypeFieldResponse
                {
                    Key = field.Key,
                    Type = field.Type,
                    Required = field.Required,
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
    public required List<UpdateContentTypeFieldRequest> Fields { get; init; }
}

public sealed class UpdateContentTypeFieldRequest
{
    public required string Key { get; init; }
    public required ContentFieldType Type { get; init; }
    public bool Required { get; init; }
    public JsonElement Settings { get; init; }
}

public sealed class UpdateContentTypeResponse
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public required string Key { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public required IReadOnlyList<UpdateContentTypeFieldResponse> Fields { get; init; }
}

public sealed class UpdateContentTypeFieldResponse
{
    public required string Key { get; init; }
    public required ContentFieldType Type { get; init; }
    public bool Required { get; init; }
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
        RuleFor(request => request.Type)
            .IsInEnum()
            .WithMessage("Type must be text, number, or boolean.");
        RuleFor(request => request.Settings)
            .Must(settings =>
                settings.ValueKind is JsonValueKind.Undefined or
                    JsonValueKind.Null or
                    JsonValueKind.Object)
            .WithMessage("Settings must be a JSON object.");
    }
}
