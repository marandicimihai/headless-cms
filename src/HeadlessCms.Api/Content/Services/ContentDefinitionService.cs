using System.Text.Json;
using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HeadlessCms.Api.Content.Services;

public sealed record ContentFieldInput(
    string Key,
    string Name,
    ContentFieldType Type,
    bool Required,
    bool Nullable,
    JsonElement Settings);

public sealed record ContentTypeDefinition(
    ContentType ContentType,
    ContentTypeVersion Version);

public class ContentDefinitionService(
    ApplicationDbContext db,
    ContentDocumentValidator documentValidator)
{
    private static readonly JsonElement EmptySettings =
        JsonSerializer.SerializeToElement(new Dictionary<string, object?>());

    public async Task<ContentTypeDefinition> CreateAsync(
        Guid tenantId,
        Guid projectId,
        string key,
        string name,
        IReadOnlyCollection<ContentFieldInput> fields,
        CancellationToken ct = default)
    {
        if (!await db.Projects.AnyAsync(
                project => project.TenantId == tenantId && project.Id == projectId,
                ct))
        {
            throw new ContentNotFoundException("Project not found.");
        }

        if (await db.ContentTypes.AnyAsync(
                candidate =>
                    candidate.TenantId == tenantId &&
                    candidate.ProjectId == projectId &&
                    candidate.Key == key,
                ct))
        {
            throw new ContentConflictException(
                $"Content type '{key}' already exists in this project.");
        }

        ValidateFields(fields);

        await using var transaction = await BeginTransactionIfSupportedAsync(ct);
        var now = DateTime.UtcNow;
        var contentType = new ContentType
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = projectId,
            Key = key,
            Name = name,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.ContentTypes.Add(contentType);
        await db.SaveChangesAsync(ct);

        var version = CreateVersion(contentType, 1, fields, now);
        db.ContentTypeVersions.Add(version);
        await db.SaveChangesAsync(ct);

        contentType.CurrentVersionId = version.Id;
        await db.SaveChangesAsync(ct);

        if (transaction is not null)
            await transaction.CommitAsync(ct);

        return new ContentTypeDefinition(contentType, version);
    }

    public async Task<ContentTypeDefinition?> UpdateAsync(
        Guid tenantId,
        Guid projectId,
        string key,
        string name,
        IReadOnlyCollection<ContentFieldInput> fields,
        CancellationToken ct = default)
    {
        ValidateFields(fields);

        var contentType = await db.ContentTypes.SingleOrDefaultAsync(
            candidate =>
                candidate.TenantId == tenantId &&
                candidate.ProjectId == projectId &&
                candidate.Key == key,
            ct);

        if (contentType is null)
            return null;

        var historicalTypes = await db.ContentFields
            .Where(field =>
                field.TenantId == tenantId &&
                field.ProjectId == projectId &&
                field.ContentTypeVersion.ContentTypeId == contentType.Id)
            .Select(field => new { field.Key, field.Type })
            .Distinct()
            .ToListAsync(ct);

        var historicalByKey = historicalTypes.ToDictionary(
            item => item.Key,
            item => item.Type,
            StringComparer.Ordinal);

        foreach (var field in fields)
        {
            if (historicalByKey.TryGetValue(field.Key, out var historicalType) &&
                historicalType != field.Type)
            {
                throw new ContentValidationException(
                    $"Field '{field.Key}' was previously declared as " +
                    $"'{historicalType.ToString().ToLowerInvariant()}' and cannot change type.");
            }
        }

        await using var transaction = await BeginTransactionIfSupportedAsync(ct);
        var nextVersion = await db.ContentTypeVersions
            .Where(version =>
                version.TenantId == tenantId &&
                version.ProjectId == projectId &&
                version.ContentTypeId == contentType.Id)
            .MaxAsync(version => version.Version, ct) + 1;

        var now = DateTime.UtcNow;
        var version = CreateVersion(contentType, nextVersion, fields, now);
        db.ContentTypeVersions.Add(version);
        await db.SaveChangesAsync(ct);

        contentType.Name = name;
        contentType.CurrentVersionId = version.Id;
        contentType.UpdatedAt = now;
        await db.SaveChangesAsync(ct);

        if (transaction is not null)
            await transaction.CommitAsync(ct);

        return new ContentTypeDefinition(contentType, version);
    }

    public async Task<ContentTypeDefinition?> GetCurrentAsync(
        Guid tenantId,
        Guid projectId,
        string key,
        CancellationToken ct = default)
    {
        var contentType = await db.ContentTypes
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.TenantId == tenantId &&
                    candidate.ProjectId == projectId &&
                    candidate.Key == key,
                ct);

        if (contentType?.CurrentVersionId is null)
            return null;

        var version = await db.ContentTypeVersions
            .AsNoTracking()
            .Include(candidate => candidate.Fields.OrderBy(field => field.Position))
            .SingleAsync(
                candidate =>
                    candidate.TenantId == tenantId &&
                    candidate.ProjectId == projectId &&
                    candidate.ContentTypeId == contentType.Id &&
                    candidate.Id == contentType.CurrentVersionId,
                ct);

        return new ContentTypeDefinition(contentType, version);
    }

    public async Task<IReadOnlyList<ContentTypeDefinition>> ListCurrentAsync(
        Guid tenantId,
        Guid projectId,
        CancellationToken ct = default)
    {
        if (!await db.Projects.AnyAsync(
                project => project.TenantId == tenantId && project.Id == projectId,
                ct))
        {
            throw new ContentNotFoundException("Project not found.");
        }

        var contentTypes = await db.ContentTypes
            .AsNoTracking()
            .Where(contentType =>
                contentType.TenantId == tenantId &&
                contentType.ProjectId == projectId)
            .OrderBy(contentType => contentType.Name)
            .ToListAsync(ct);

        if (contentTypes.Count == 0)
            return [];

        var currentVersionIds = contentTypes
            .Where(contentType => contentType.CurrentVersionId.HasValue)
            .Select(contentType => contentType.CurrentVersionId!.Value)
            .ToList();

        var versions = await db.ContentTypeVersions
            .AsNoTracking()
            .Include(version => version.Fields.OrderBy(field => field.Position))
            .Where(version =>
                version.TenantId == tenantId &&
                version.ProjectId == projectId &&
                currentVersionIds.Contains(version.Id))
            .ToDictionaryAsync(version => version.Id, ct);

        return contentTypes
            .Where(contentType =>
                contentType.CurrentVersionId.HasValue &&
                versions.ContainsKey(contentType.CurrentVersionId.Value))
            .Select(contentType => new ContentTypeDefinition(
                contentType,
                versions[contentType.CurrentVersionId!.Value]))
            .ToList();
    }

    private void ValidateFields(IReadOnlyCollection<ContentFieldInput> fields)
    {
        if (fields.Count == 0)
            throw new ContentValidationException("At least one field is required.");

        var duplicate = fields
            .GroupBy(field => field.Key, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
            throw new ContentValidationException($"Field key '{duplicate.Key}' is duplicated.");

        foreach (var field in fields)
        {
            var settings = NormalizeSettings(field.Settings);
            documentValidator.ValidateFieldSettings(
                field.Type,
                field.Nullable,
                field.Key,
                settings);
        }
    }

    private static ContentTypeVersion CreateVersion(
        ContentType contentType,
        int versionNumber,
        IReadOnlyCollection<ContentFieldInput> fields,
        DateTime createdAt)
    {
        var version = new ContentTypeVersion
        {
            Id = Guid.NewGuid(),
            TenantId = contentType.TenantId,
            ProjectId = contentType.ProjectId,
            ContentTypeId = contentType.Id,
            Version = versionNumber,
            CreatedAt = createdAt
        };

        version.Fields = fields
            .Select((field, position) => new ContentField
            {
                Id = Guid.NewGuid(),
                TenantId = contentType.TenantId,
                ProjectId = contentType.ProjectId,
                ContentTypeVersionId = version.Id,
                Key = field.Key,
                Name = field.Name,
                Type = field.Type,
                Required = field.Required,
                Nullable = field.Nullable,
                Position = position,
                Settings = NormalizeSettings(field.Settings)
            })
            .ToList();

        return version;
    }

    private static JsonElement NormalizeSettings(JsonElement settings) =>
        settings.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
            ? EmptySettings.Clone()
            : settings.Clone();

    private async Task<IDbContextTransaction?> BeginTransactionIfSupportedAsync(
        CancellationToken ct)
    {
        return db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(ct)
            : null;
    }
}
