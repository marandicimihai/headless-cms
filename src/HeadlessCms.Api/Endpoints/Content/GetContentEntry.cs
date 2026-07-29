using System.Text.Json;
using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Content.Services;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Tenancy.Models;
using HeadlessCms.Api.Tenancy.Services;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Endpoints.Content;

public sealed class GetContentEntry(
    ContentEntryService entries,
    ApplicationDbContext db,
    TenantAccessService tenantAccess)
    : Endpoint<GetContentEntryRequest, GetContentEntryResponse>
{
    private static readonly IReadOnlySet<TenantRole> Readers =
        new HashSet<TenantRole>([TenantRole.Owner, TenantRole.Editor, TenantRole.Member]);

    public override void Configure()
    {
        Get(
            "tenants/{tenantId:guid}/projects/{projectId:guid}/" +
            "content-types/{contentTypeKey}/entries/{entryId:guid}");
        Claims("sub");
    }

    public override async Task HandleAsync(GetContentEntryRequest request, CancellationToken ct)
    {
        if (await tenantAccess.ResolveAsync(User, request.TenantId, Readers, ct) is null)
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        var entry = await entries.GetAsync(
            request.TenantId,
            request.ProjectId,
            request.ContentTypeKey,
            request.EntryId,
            ct);

        if (entry is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var schemaVersion = await db.ContentTypeVersions
            .Where(version =>
                version.TenantId == request.TenantId &&
                version.ProjectId == request.ProjectId &&
                version.Id == entry.ContentTypeVersionId)
            .Select(version => version.Version)
            .SingleAsync(ct);

        Response = ToResponse(entry, schemaVersion);
        entry.Dispose();
    }

    private static GetContentEntryResponse ToResponse(
        ContentEntry entry,
        int schemaVersion) =>
        new()
        {
            Id = entry.Id,
            SchemaVersion = schemaVersion,
            Status = entry.Status.ToString().ToLowerInvariant(),
            Data = entry.Data.RootElement.Clone(),
            CreatedAt = entry.CreatedAt,
            UpdatedAt = entry.UpdatedAt
        };
}

public sealed class GetContentEntryRequest
{
    public Guid TenantId { get; init; }
    public Guid ProjectId { get; init; }
    public string ContentTypeKey { get; init; } = default!;
    public Guid EntryId { get; init; }
}

public sealed class GetContentEntryResponse
{
    public Guid Id { get; init; }
    public int SchemaVersion { get; init; }
    public required string Status { get; init; }
    public JsonElement Data { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
