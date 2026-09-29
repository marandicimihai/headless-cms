using System.Text.Json;
using FluentValidation;
using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Content.Services;
using HeadlessCms.Api.Workspaces.Services;

using HeadlessCms.Api.Documentation;

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
        Description(b => b.WithTags("Content types"), clearDefaults: true);
        Summary((EndpointSummary s) =>
        {
            s.Summary = "Update a content type";
            s.Description = "Access: Workspace Owner or Editor.\n\nReplaces the field list and order. The content-type key is immutable. Existing field types cannot change. Removed fields are removed from entries; surviving data is revalidated against the new definition. Invalid migrations return 400.";
            s.Params["WorkspaceId"] = "Workspace UUID.";
            s.Params["ProjectId"] = "Project UUID within the workspace.";
            s.Params["ContentTypeKey"] = "Immutable content-type key, for example articles.";
            s.Params["Fields"] = "Complete ordered field definitions. Each field requires a unique key and a text, number, or boolean type.";
            s.ExampleRequest = new { Fields = ApiExamples.Fields };
            s.Response<UpdateContentTypeResponse>(200, "Success.");
            s.ResponseExamples[200] = ApiExamples.ContentType;
            s.Response<FastEndpoints.ProblemDetails>(400, "Invalid request or validation failure.", "application/problem+json");
            s.Response<ApiProblem>(401, "Authentication is required, or the session has expired.", "application/problem+json");
            s.Response(403, "The caller does not have permission.");
            s.Response(404, "Resource not found or inaccessible.");
        });
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

    private static UpdateContentTypeResponse ToResponse(ContentType definition) =>
        new()
        {
            Id = definition.Id,
            ProjectId = definition.ProjectId,
            Key = definition.Key,
            CreatedAt = definition.CreatedAt,
            UpdatedAt = definition.UpdatedAt,
            Fields = definition.Fields
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
