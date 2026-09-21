using System.Text.Json;
using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Content.Services;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;

namespace HeadlessCms.Api.Endpoints.Content;

public sealed class ListContentTypes(
    ContentDefinitionService definitions,
    WorkspaceAccessService workspaceAccess)
    : Endpoint<ListContentTypesRequest, IReadOnlyList<ListContentTypesItemResponse>>
{
    public override void Configure()
    {
        Get("workspaces/{workspaceId:guid}/projects/{projectId:guid}/content-types");
        Claims("sub");
    }

    public override async Task HandleAsync(ListContentTypesRequest request, CancellationToken ct)
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

        try
        {
            Response = (await definitions.ListCurrentAsync(
                    request.WorkspaceId,
                    request.ProjectId,
                    ct))
                .Select(ToResponse)
                .ToList();
        }
        catch (ContentNotFoundException)
        {
            await Send.NotFoundAsync(ct);
        }
    }

    private static ListContentTypesItemResponse ToResponse(
        ContentType definition) =>
        new()
        {
            Id = definition.Id,
            ProjectId = definition.ProjectId,
            Key = definition.Key,
            CreatedAt = definition.CreatedAt,
            UpdatedAt = definition.UpdatedAt,
            Fields = definition.Fields
                .OrderBy(field => field.Position)
                .Select(field => new ListContentTypesFieldResponse
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

public sealed class ListContentTypesRequest
{
    public Guid WorkspaceId { get; init; }
    public Guid ProjectId { get; init; }
}

public sealed class ListContentTypesItemResponse
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public required string Key { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public required IReadOnlyList<ListContentTypesFieldResponse> Fields { get; init; }
}

public sealed class ListContentTypesFieldResponse
{
    public required string Key { get; init; }
    public required ContentFieldType Type { get; init; }
    public bool Required { get; init; }
    public int Position { get; init; }
    public JsonElement Settings { get; init; }
}
