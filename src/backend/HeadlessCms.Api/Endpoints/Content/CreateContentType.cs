using System.Text.Json;
using FluentValidation;
using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Content.Services;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;

using HeadlessCms.Api.Documentation;

namespace HeadlessCms.Api.Endpoints.Content;

public sealed class CreateContentType(
    ContentDefinitionService definitions,
    WorkspaceAccessService workspaceAccess)
    : Endpoint<CreateContentTypeRequest, CreateContentTypeResponse>
{
    private const string KeyPattern = "^[a-z][a-z0-9_]*$";

    public override void Configure()
    {
        Post("workspaces/{workspaceId:guid}/projects/{projectId:guid}/content-types");
        Claims("sub");
        Description(b => b.WithTags("Content types"), clearDefaults: true);
        Summary((EndpointSummary s) =>
        {
            s.Summary = "Create a content type";
            s.Description = "Access: Workspace Owner or Editor.\n\nKeys must match ^[a-z][a-z0-9_]*$ and contain at most 64 characters. Supply at least one field. Types are text, number, and boolean. settings.default is allowed only for optional fields and must match the type.";
            s.Params["WorkspaceId"] = "Workspace UUID.";
            s.Params["ProjectId"] = "Project UUID within the workspace.";
            s.Params["Key"] = "Immutable key matching ^[a-z][a-z0-9_]*$, maximum 64 characters.";
            s.Params["Fields"] = "Complete ordered field definitions. Each field requires a unique key and a text, number, or boolean type.";
            s.ExampleRequest = new { Key = "articles", Fields = ApiExamples.Fields };
            s.Response<CreateContentTypeResponse>(201, "Created.");
            s.ResponseExamples[201] = ApiExamples.ContentType;
            s.Response<FastEndpoints.ProblemDetails>(400, "Invalid request or validation failure.", "application/problem+json");
            s.Response<ApiProblem>(401, "Authentication is required, or the session has expired.", "application/problem+json");
            s.Response(403, "The caller does not have permission.");
            s.Response(404, "Resource not found or inaccessible.");
            s.Response<string>(409, "The content-type key already exists.", "text/plain");
        });
    }

    public override async Task HandleAsync(CreateContentTypeRequest request, CancellationToken ct)
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
            var definition = await definitions.CreateAsync(
                request.WorkspaceId,
                request.ProjectId,
                request.Key,
                ToInputs(request.Fields),
                ct);

            await Send.CreatedAtAsync<GetContentType>(
                new
                {
                    request.WorkspaceId,
                    request.ProjectId,
                    ContentTypeKey = request.Key
                },
                ToResponse(definition),
                cancellation: ct);
        }
        catch (ContentNotFoundException)
        {
            await Send.NotFoundAsync(ct);
        }
        catch (ContentConflictException exception)
        {
            await Send.StringAsync(exception.Message, 409, cancellation: ct);
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
        IEnumerable<CreateContentTypeFieldRequest> fields) =>
        fields.Select(field => new ContentFieldInput(
                field.Key,
                field.Type,
                field.Required,
                field.Settings))
            .ToList();

    private static CreateContentTypeResponse ToResponse(ContentType definition) =>
        new()
        {
            Id = definition.Id,
            ProjectId = definition.ProjectId,
            Key = definition.Key,
            CreatedAt = definition.CreatedAt,
            UpdatedAt = definition.UpdatedAt,
            Fields = definition.Fields
                .OrderBy(field => field.Position)
                .Select(field => new CreateContentTypeFieldResponse
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

public sealed class CreateContentTypeRequest
{
    public Guid WorkspaceId { get; init; }
    public Guid ProjectId { get; init; }
    public required string Key { get; init; }
    public required List<CreateContentTypeFieldRequest> Fields { get; init; }
}

public sealed class CreateContentTypeFieldRequest
{
    public required string Key { get; init; }
    public required ContentFieldType Type { get; init; }
    public bool Required { get; init; }
    public JsonElement Settings { get; init; }
}

public sealed class CreateContentTypeResponse
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public required string Key { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public required IReadOnlyList<CreateContentTypeFieldResponse> Fields { get; init; }
}

public sealed class CreateContentTypeFieldResponse
{
    public required string Key { get; init; }
    public required ContentFieldType Type { get; init; }
    public bool Required { get; init; }
    public int Position { get; init; }
    public JsonElement Settings { get; init; }
}

public sealed class CreateContentTypeRequestValidator : Validator<CreateContentTypeRequest>
{
    public CreateContentTypeRequestValidator()
    {
        RuleFor(request => request.Key)
            .NotEmpty()
            .MaximumLength(64)
            .Matches(CreateContentType.FieldKeyPattern);
        RuleFor(request => request.Fields).NotEmpty();
        RuleForEach(request => request.Fields)
            .SetValidator(new CreateContentTypeFieldRequestValidator());
    }
}

public sealed class CreateContentTypeFieldRequestValidator
    : Validator<CreateContentTypeFieldRequest>
{
    public CreateContentTypeFieldRequestValidator()
    {
        RuleFor(request => request.Key)
            .NotEmpty()
            .MaximumLength(64)
            .Matches(CreateContentType.FieldKeyPattern);
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
