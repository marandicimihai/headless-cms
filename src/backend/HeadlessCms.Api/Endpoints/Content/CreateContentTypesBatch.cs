using System.Text.Json;
using System.Text.RegularExpressions;
using FastEndpoints;
using FluentValidation;
using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Content.Services;
using HeadlessCms.Api.Endpoints;
using HeadlessCms.Api.Workspaces.Services;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Endpoints.Content;

public sealed class CreateContentTypesBatch(
    ContentDefinitionService definitions,
    WorkspaceAccessService workspaceAccess)
    : Endpoint<CreateContentTypesBatchRequest, CreateContentTypesBatchResponse>
{
    private static readonly Regex KeyPattern = new("^[a-z][a-z0-9_]{0,63}$", RegexOptions.Compiled);

    public override void Configure()
    {
        Post("workspaces/{workspaceId:guid}/projects/{projectId:guid}/content-types/batch");
        Claims("sub");
        Description(b => b.WithTags("Content types"), clearDefaults: true);
        Summary(s =>
        {
            s.Summary = "Create several content types atomically";
            s.Description = "Access: Workspace Owner or Editor. Creates the complete batch only when every content type is valid and all keys are available.";
            s.Response<CreateContentTypesBatchResponse>(201, "All content types were created.");
            s.Response<FastEndpoints.ProblemDetails>(400, "Invalid request or field definition.", "application/problem+json");
            s.Response<ApiProblem>(401, "Authentication is required.", "application/problem+json");
            s.Response(403, "The caller does not have permission.");
            s.Response(404, "Project not found or inaccessible.");
            s.Response<ApiProblem>(409, "A content type key already exists.", "application/problem+json");
        });
    }

    public override async Task HandleAsync(CreateContentTypesBatchRequest request, CancellationToken ct)
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

        if (request.ContentTypes is null || request.ContentTypes.Count is 0 or > 6)
        {
            AddError(r => r.ContentTypes, "Supply between 1 and 6 content types.");
            await Send.ErrorsAsync(cancellation: ct);
            return;
        }

        var duplicateKey = request.ContentTypes
            .GroupBy(type => type.Key, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicateKey is not null)
        {
            await ApiErrors.SendAsync(
                HttpContext,
                409,
                "CONTENT_TYPE_CONFLICT",
                $"Content type '{duplicateKey}' occurs more than once in the request.",
                ct);
            return;
        }

        foreach (var type in request.ContentTypes)
        {
            if (string.IsNullOrWhiteSpace(type.Key) || !KeyPattern.IsMatch(type.Key))
            {
                AddError(r => r.ContentTypes, "Content type keys must match ^[a-z][a-z0-9_]*$ and be at most 64 characters.");
                break;
            }
            if (type.Fields is null || type.Fields.Count is 0 or > 20)
            {
                AddError(r => r.ContentTypes, $"Content type '{type.Key}' must have between 1 and 20 fields.");
                break;
            }
            if (type.Fields.Any(field => string.IsNullOrWhiteSpace(field.Key) || !KeyPattern.IsMatch(field.Key)))
            {
                AddError(r => r.ContentTypes, $"Fields in '{type.Key}' must have keys matching ^[a-z][a-z0-9_]*$.");
                break;
            }
        }
        if (ValidationFailed)
        {
            await Send.ErrorsAsync(cancellation: ct);
            return;
        }

        try
        {
            var contentTypes = await definitions.CreateManyAsync(
                request.WorkspaceId,
                request.ProjectId,
                request.ContentTypes.Select(type => new ContentTypeDefinitionInput(
                    type.Key,
                    type.Fields.Select(field => new ContentFieldInput(
                        field.Key,
                        field.Type,
                        field.Required,
                        field.Settings.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
                            ? JsonSerializer.SerializeToElement(new Dictionary<string, object?>())
                            : field.Settings.Clone())).ToList())).ToList(),
                ct);

            await Send.ResponseAsync(new CreateContentTypesBatchResponse
            {
                ContentTypes = contentTypes.Select(ToResponse).ToList()
            }, StatusCodes.Status201Created, ct);
        }
        catch (ContentNotFoundException)
        {
            await Send.NotFoundAsync(ct);
        }
        catch (ContentConflictException exception)
        {
            await ApiErrors.SendAsync(HttpContext, 409, "CONTENT_TYPE_CONFLICT", exception.Message, ct);
        }
        catch (ContentValidationException exception)
        {
            foreach (var error in exception.Errors)
                AddError(error);
            await Send.ErrorsAsync(cancellation: ct);
        }
        catch (DbUpdateException)
        {
            await ApiErrors.SendAsync(
                HttpContext,
                409,
                "CONTENT_TYPE_CONFLICT",
                "A content type with one of these keys already exists.",
                ct);
        }
    }

    private static CreateContentTypeResponse ToResponse(ContentType definition) => new()
    {
        Id = definition.Id,
        ProjectId = definition.ProjectId,
        Key = definition.Key,
        CreatedAt = definition.CreatedAt,
        UpdatedAt = definition.UpdatedAt,
        Fields = definition.Fields.OrderBy(field => field.Position)
            .Select(field => new CreateContentTypeFieldResponse
            {
                Key = field.Key,
                Type = field.Type,
                Required = field.Required,
                Position = field.Position,
                Settings = field.Settings.Clone()
            }).ToList()
    };
}

public sealed class CreateContentTypesBatchRequest
{
    public Guid WorkspaceId { get; init; }
    public Guid ProjectId { get; init; }
    public required List<CreateContentTypeBatchItemRequest> ContentTypes { get; init; }
}

public sealed class CreateContentTypeBatchItemRequest
{
    public required string Key { get; init; }
    public required List<CreateContentTypeFieldRequest> Fields { get; init; }
}

public sealed class CreateContentTypesBatchResponse
{
    public required IReadOnlyList<CreateContentTypeResponse> ContentTypes { get; init; }
}

public sealed class CreateContentTypesBatchRequestValidator : Validator<CreateContentTypesBatchRequest>
{
    public CreateContentTypesBatchRequestValidator()
    {
        RuleFor(request => request.ContentTypes).NotEmpty().Must(types => types.Count <= 6);
        RuleForEach(request => request.ContentTypes).ChildRules(type =>
        {
            type.RuleFor(item => item.Key).NotEmpty().MaximumLength(64).Matches("^[a-z][a-z0-9_]*$");
            type.RuleFor(item => item.Fields).NotEmpty().Must(fields => fields.Count <= 20);
            type.RuleForEach(item => item.Fields).SetValidator(new CreateContentTypeFieldRequestValidator());
        });
    }
}
