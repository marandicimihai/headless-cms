using System.Text.Json;
using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Content.Services;
using HeadlessCms.Api.Workspaces.Services;

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
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        Response = ToResponse(definition);
    }

    private static GetContentTypeResponse ToResponse(ContentTypeDefinition definition) =>
        new()
        {
            Id = definition.ContentType.Id,
            ProjectId = definition.ContentType.ProjectId,
            Key = definition.ContentType.Key,
            CreatedAt = definition.ContentType.CreatedAt,
            UpdatedAt = definition.ContentType.UpdatedAt,
            Fields = definition.ContentType.Fields
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
