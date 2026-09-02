using System.Text.Json;
using System.Text.Json.Nodes;
using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HeadlessCms.Api.Content.Services;

public sealed record ContentFieldInput(
    string Key,
    ContentFieldType Type,
    bool Required,
    bool Nullable,
    JsonElement Settings);

public sealed record ContentTypeDefinition(ContentType ContentType);

public class ContentDefinitionService(
    ApplicationDbContext db,
    ContentDocumentValidator documentValidator)
{
    private static readonly JsonElement EmptySettings =
        JsonSerializer.SerializeToElement(new Dictionary<string, object?>());

    public async Task<ContentTypeDefinition> CreateAsync(
        Guid workspaceId,
        Guid projectId,
        string key,
        IReadOnlyCollection<ContentFieldInput> fields,
        CancellationToken ct = default)
    {
        if (!await db.Projects.AnyAsync(
                project => project.WorkspaceId == workspaceId && project.Id == projectId,
                ct))
        {
            throw new ContentNotFoundException("Project not found.");
        }

        if (await db.ContentTypes.AnyAsync(
                candidate =>
                    candidate.WorkspaceId == workspaceId &&
                    candidate.ProjectId == projectId &&
                    candidate.Key == key,
                ct))
        {
            throw new ContentConflictException(
                $"Content type '{key}' already exists in this project.");
        }

        var normalizedFields = ValidateAndCreateFields(workspaceId, projectId, fields);
        var now = DateTime.UtcNow;
        var contentType = new ContentType
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            ProjectId = projectId,
            Key = key,
            CreatedAt = now,
            UpdatedAt = now
        };

        foreach (var field in normalizedFields)
        {
            field.ContentTypeId = contentType.Id;
            contentType.Fields.Add(field);
        }

        db.ContentTypes.Add(contentType);
        await db.SaveChangesAsync(ct);
        return new ContentTypeDefinition(contentType);
    }

    public async Task<ContentTypeDefinition?> UpdateAsync(
        Guid workspaceId,
        Guid projectId,
        string key,
        IReadOnlyCollection<ContentFieldInput> fields,
        CancellationToken ct = default)
    {
        var proposedFields = ValidateAndCreateFields(workspaceId, projectId, fields);
        var contentType = await db.ContentTypes
            .Include(candidate => candidate.Fields)
            .Include(candidate => candidate.Entries)
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.WorkspaceId == workspaceId &&
                    candidate.ProjectId == projectId &&
                    candidate.Key == key,
                ct);

        if (contentType is null)
            return null;

        var existingByKey = contentType.Fields.ToDictionary(
            field => field.Key,
            StringComparer.Ordinal);

        foreach (var proposed in proposedFields)
        {
            if (existingByKey.TryGetValue(proposed.Key, out var existing) &&
                existing.Type != proposed.Type)
            {
                throw new ContentValidationException(
                    $"Field '{proposed.Key}' is declared as " +
                    $"'{existing.Type.ToString().ToLowerInvariant()}' and cannot change type.");
            }
        }

        await using var transaction = await BeginTransactionIfSupportedAsync(ct);
        var proposedKeys = proposedFields
            .Select(field => field.Key)
            .ToHashSet(StringComparer.Ordinal);
        var now = DateTime.UtcNow;
        var replacedDocuments = new List<JsonDocument>();

        foreach (var entry in contentType.Entries)
        {
            var migratedData = JsonNode.Parse(entry.Data.RootElement.GetRawText())?.AsObject()
                ?? throw new ContentValidationException("Entry data must be a JSON object.");

            foreach (var propertyName in migratedData.Select(property => property.Key).ToList())
            {
                if (!proposedKeys.Contains(propertyName))
                    migratedData.Remove(propertyName);
            }

            JsonDocument validatedData;
            try
            {
                validatedData = documentValidator.Validate(
                    JsonSerializer.SerializeToElement(migratedData),
                    proposedFields);
            }
            catch (ContentValidationException exception)
            {
                throw new ContentValidationException(
                    exception.Errors
                        .Select(error => $"Entry '{entry.Id}': {error}")
                        .ToList());
            }

            if (entry.Data.RootElement.GetRawText() == validatedData.RootElement.GetRawText())
            {
                validatedData.Dispose();
                continue;
            }

            var previousData = entry.Data;
            entry.Data = validatedData;
            entry.UpdatedAt = now;
            replacedDocuments.Add(previousData);
        }

        var removedFields = contentType.Fields
            .Where(field => !proposedKeys.Contains(field.Key))
            .ToList();
        db.ContentFields.RemoveRange(removedFields);

        foreach (var proposed in proposedFields)
        {
            if (existingByKey.TryGetValue(proposed.Key, out var existing))
            {
                existing.Required = proposed.Required;
                existing.Nullable = proposed.Nullable;
                existing.Position = proposed.Position;
                existing.Settings = proposed.Settings.Clone();
                continue;
            }

            proposed.ContentTypeId = contentType.Id;
            proposed.ContentType = contentType;
            contentType.Fields.Add(proposed);
            db.ContentFields.Add(proposed);
        }

        contentType.UpdatedAt = now;
        await db.SaveChangesAsync(ct);

        if (transaction is not null)
            await transaction.CommitAsync(ct);

        foreach (var document in replacedDocuments)
            document.Dispose();

        contentType.Fields = contentType.Fields
            .Where(field => proposedKeys.Contains(field.Key))
            .OrderBy(field => field.Position)
            .ToList();
        return new ContentTypeDefinition(contentType);
    }

    public async Task<bool> DeleteAsync(
        Guid workspaceId,
        Guid projectId,
        string key,
        CancellationToken ct = default)
    {
        var contentType = await db.ContentTypes.SingleOrDefaultAsync(
            candidate =>
                candidate.WorkspaceId == workspaceId &&
                candidate.ProjectId == projectId &&
                candidate.Key == key,
            ct);

        if (contentType is null)
            return false;

        db.ContentTypes.Remove(contentType);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ContentTypeDefinition?> GetCurrentAsync(
        Guid workspaceId,
        Guid projectId,
        string key,
        CancellationToken ct = default)
    {
        var contentType = await db.ContentTypes
            .AsNoTracking()
            .Include(candidate => candidate.Fields.OrderBy(field => field.Position))
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.WorkspaceId == workspaceId &&
                    candidate.ProjectId == projectId &&
                    candidate.Key == key,
                ct);

        return contentType is null ? null : new ContentTypeDefinition(contentType);
    }

    public async Task<IReadOnlyList<ContentTypeDefinition>> ListCurrentAsync(
        Guid workspaceId,
        Guid projectId,
        CancellationToken ct = default)
    {
        if (!await db.Projects.AnyAsync(
                project => project.WorkspaceId == workspaceId && project.Id == projectId,
                ct))
        {
            throw new ContentNotFoundException("Project not found.");
        }

        return (await db.ContentTypes
                .AsNoTracking()
                .Include(contentType => contentType.Fields.OrderBy(field => field.Position))
                .Where(contentType =>
                    contentType.WorkspaceId == workspaceId &&
                    contentType.ProjectId == projectId)
                .OrderBy(contentType => contentType.Key)
                .ToListAsync(ct))
            .Select(contentType => new ContentTypeDefinition(contentType))
            .ToList();
    }

    private List<ContentField> ValidateAndCreateFields(
        Guid workspaceId,
        Guid projectId,
        IReadOnlyCollection<ContentFieldInput> fields)
    {
        if (fields.Count == 0)
            throw new ContentValidationException("At least one field is required.");

        var duplicate = fields
            .GroupBy(field => field.Key, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
            throw new ContentValidationException($"Field key '{duplicate.Key}' is duplicated.");

        return fields
            .Select((field, position) =>
            {
                var settings = NormalizeSettings(field.Settings);
                documentValidator.ValidateFieldSettings(
                    field.Type,
                    field.Nullable,
                    field.Key,
                    settings);

                return new ContentField
                {
                    Id = Guid.NewGuid(),
                    WorkspaceId = workspaceId,
                    ProjectId = projectId,
                    Key = field.Key,
                    Type = field.Type,
                    Required = field.Required,
                    Nullable = field.Nullable,
                    Position = position,
                    Settings = settings
                };
            })
            .ToList();
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
