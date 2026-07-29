using System.Text.Json;
using HeadlessCms.Api.Content.Services;
using HeadlessCms.Api.Tenancy.Models;
using HeadlessCms.Api.Tenancy.Services;

namespace HeadlessCms.Api.Endpoints.Content;

public sealed class GetContentType(
    ContentDefinitionService definitions,
    TenantAccessService tenantAccess)
    : Endpoint<GetContentTypeRequest, GetContentTypeResponse>
{
    private static readonly IReadOnlySet<TenantRole> Readers =
        new HashSet<TenantRole>([TenantRole.Owner, TenantRole.Editor, TenantRole.Member]);

    public override void Configure()
    {
        Get(
            "tenants/{tenantId:guid}/projects/{projectId:guid}/" +
            "content-types/{contentTypeKey}");
        Claims("sub");
    }

    public override async Task HandleAsync(GetContentTypeRequest request, CancellationToken ct)
    {
        if (await tenantAccess.ResolveAsync(User, request.TenantId, Readers, ct) is null)
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        var definition = await definitions.GetCurrentAsync(
            request.TenantId,
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
            Name = definition.ContentType.Name,
            Version = definition.Version.Version,
            CreatedAt = definition.ContentType.CreatedAt,
            UpdatedAt = definition.ContentType.UpdatedAt,
            Fields = definition.Version.Fields
                .OrderBy(field => field.Position)
                .Select(field => new GetContentTypeFieldResponse
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

public sealed class GetContentTypeRequest
{
    public Guid TenantId { get; init; }
    public Guid ProjectId { get; init; }
    public string ContentTypeKey { get; init; } = default!;
}

public sealed class GetContentTypeResponse
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public required string Key { get; init; }
    public required string Name { get; init; }
    public int Version { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public required IReadOnlyList<GetContentTypeFieldResponse> Fields { get; init; }
}

public sealed class GetContentTypeFieldResponse
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required string Type { get; init; }
    public bool Required { get; init; }
    public bool Nullable { get; init; }
    public int Position { get; init; }
    public JsonElement Settings { get; init; }
}
