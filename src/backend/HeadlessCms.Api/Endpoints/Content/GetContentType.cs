using System.Text.Json;
using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Content.Services;
using HeadlessCms.Api.Workspaces.Services;

using HeadlessCms.Api.Documentation;

namespace HeadlessCms.Api.Endpoints.Content;

public sealed class GetContentType(
    ContentDefinitionService definitions,
    WorkspaceAccessService workspaceAccess)
    : Endpoint<GetContentTypeRequest, GetContentTypeResponse>
{
    public override void Configure()
    {
        Get(
            "workspaces/{workspaceId:guid}/projects/{projectId:guid}/" +
            "content-types/{contentTypeKey}");
        Claims("sub");
        Description(b => b.WithTags("Content types"), clearDefaults: true);
        Summary((EndpointSummary s) =>
        {
            s.Summary = "Get a content type";
            s.Description = "Access: Workspace Owner, Editor, or Member.\n\nReturns the current definition identified by its key within the project.";
            s.Params["WorkspaceId"] = "Workspace UUID.";
            s.Params["ProjectId"] = "Project UUID within the workspace.";
            s.Params["ContentTypeKey"] = "Immutable content-type key, for example articles.";
            s.Response<GetContentTypeResponse>(200, "Success.");
            s.ResponseExamples[200] = ApiExamples.ContentType;
            s.Response<FastEndpoints.ProblemDetails>(400, "Invalid request or validation failure.", "application/problem+json");
            s.Response<ApiProblem>(401, "Authentication is required, or the session has expired.", "application/problem+json");
            s.Response(403, "The caller does not have permission.");
            s.Response(404, "Resource not found or inaccessible.");
        });
    }

    public override async Task HandleAsync(GetContentTypeRequest request, CancellationToken ct)
    {
        if (await workspaceAccess.ResolveAsync(
                User,
                request.WorkspaceId,
                WorkspaceAccessRoles.Members,
                ct) is null)
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        var definition = await definitions.GetCurrentAsync(
            request.WorkspaceId,
            request.ProjectId,
            request.ContentTypeKey,
            ct);

        if (definition is null)
            await Send.NotFoundAsync(ct);
        else
            Response = ToResponse(definition);
    }

    private static GetContentTypeResponse ToResponse(ContentType definition) =>
        new()
        {
            Id = definition.Id,
            ProjectId = definition.ProjectId,
            Key = definition.Key,
            CreatedAt = definition.CreatedAt,
            UpdatedAt = definition.UpdatedAt,
            Fields = definition.Fields
                .OrderBy(field => field.Position)
                .Select(field => new GetContentTypeFieldResponse
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

public sealed class GetContentTypeRequest
{
    public Guid WorkspaceId { get; init; }
    public Guid ProjectId { get; init; }
    public string ContentTypeKey { get; init; } = default!;
}

public sealed class GetContentTypeResponse
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public required string Key { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public required IReadOnlyList<GetContentTypeFieldResponse> Fields { get; init; }
}

public sealed class GetContentTypeFieldResponse
{
    public required string Key { get; init; }
    public required ContentFieldType Type { get; init; }
    public bool Required { get; init; }
    public int Position { get; init; }
    public JsonElement Settings { get; init; }
}
