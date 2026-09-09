using System.Globalization;
using System.Text.Json;
using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Content.Services;

public sealed record ContentFilter(string FieldKey, string Operator, string Value);

public sealed record ContentEntryQuery(
    IReadOnlyCollection<ContentFilter> Filters,
    string? Sort,
    ContentEntryStatus? Status,
    int Page,
    int PageSize);

public sealed record ContentEntryPage(
    IReadOnlyList<ContentEntry> Items,
    int Total,
    int Page,
    int PageSize);

public class ContentEntryService(
    ApplicationDbContext db,
    ContentDefinitionService definitions,
    ContentDocumentValidator documentValidator)
{
    public async Task<ContentEntry?> CreateAsync(
        Guid workspaceId,
        Guid projectId,
        string contentTypeKey,
        JsonElement data,
        ContentEntryStatus status,
        CancellationToken ct = default)
    {
        var definition = await definitions.GetCurrentAsync(
            workspaceId,
            projectId,
            contentTypeKey,
            ct);
        if (definition is null)
            return null;

        var validatedData = documentValidator.Validate(data, definition.ContentType.Fields);
        var now = DateTime.UtcNow;
        var entry = new ContentEntry
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            ProjectId = projectId,
            ContentTypeId = definition.ContentType.Id,
            Data = validatedData,
            Status = status,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.ContentEntries.Add(entry);
        await db.SaveChangesAsync(ct);
        return entry;
    }

    public Task<ContentEntry?> GetAsync(
        Guid workspaceId,
        Guid projectId,
        string contentTypeKey,
        Guid entryId,
        CancellationToken ct = default)
    {
        return db.ContentEntries
            .AsNoTracking()
            .SingleOrDefaultAsync(
                entry =>
                    entry.WorkspaceId == workspaceId &&
                    entry.ProjectId == projectId &&
                    entry.ContentType.Key == contentTypeKey &&
                    entry.Id == entryId,
                ct);
    }

    public async Task<ContentEntry?> UpdateAsync(
        Guid workspaceId,
        Guid projectId,
        string contentTypeKey,
        Guid entryId,
        JsonElement data,
        ContentEntryStatus status,
        CancellationToken ct = default)
    {
        var entry = await db.ContentEntries.SingleOrDefaultAsync(
            candidate =>
                candidate.WorkspaceId == workspaceId &&
                candidate.ProjectId == projectId &&
                candidate.ContentType.Key == contentTypeKey &&
                candidate.Id == entryId,
            ct);

        if (entry is null)
            return null;

        var fields = await db.ContentFields
            .AsNoTracking()
            .Where(field =>
                field.WorkspaceId == workspaceId &&
                field.ProjectId == projectId &&
                field.ContentTypeId == entry.ContentTypeId)
            .OrderBy(field => field.Position)
            .ToListAsync(ct);

        var validatedData = documentValidator.Validate(data, fields);
        var previousData = entry.Data;
        entry.Data = validatedData;
        entry.Status = status;
        entry.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        previousData.Dispose();
        return entry;
    }

    public async Task<bool> DeleteAsync(
        Guid workspaceId,
        Guid projectId,
        string contentTypeKey,
        Guid entryId,
        CancellationToken ct = default)
    {
        var entry = await db.ContentEntries.SingleOrDefaultAsync(
            candidate =>
                candidate.WorkspaceId == workspaceId &&
                candidate.ProjectId == projectId &&
                candidate.ContentType.Key == contentTypeKey &&
                candidate.Id == entryId,
            ct);

        if (entry is null)
            return false;

        db.ContentEntries.Remove(entry);
        await db.SaveChangesAsync(ct);
        entry.Dispose();
        return true;
    }

    public async Task<ContentEntryPage?> QueryAsync(
        Guid workspaceId,
        Guid projectId,
        string contentTypeKey,
        ContentEntryQuery options,
        CancellationToken ct = default)
    {
        var definition = await definitions.GetCurrentAsync(
            workspaceId,
            projectId,
            contentTypeKey,
            ct);
        if (definition is null)
            return null;

        var fields = definition.ContentType.Fields.ToDictionary(
            field => field.Key,
            StringComparer.Ordinal);

        IQueryable<ContentEntry> query = db.ContentEntries
            .AsNoTracking()
            .Where(entry =>
                entry.WorkspaceId == workspaceId &&
                entry.ProjectId == projectId &&
                entry.ContentTypeId == definition.ContentType.Id);

        if (options.Status.HasValue)
            query = query.Where(entry => entry.Status == options.Status.Value);

        foreach (var filter in options.Filters)
        {
            if (filter.FieldKey.StartsWith('$'))
            {
                query = ApplySystemFilter(query, filter);
                continue;
            }

            if (!fields.TryGetValue(filter.FieldKey, out var field))
            {
                throw new ContentValidationException(
                    $"Field '{filter.FieldKey}' is not declared by the content type.");
            }

            query = ApplyFilter(query, field, filter);
        }

        var total = await query.CountAsync(ct);
        query = ApplySort(query, fields, options.Sort);

        var items = await query
            .Skip((options.Page - 1) * options.PageSize)
            .Take(options.PageSize)
            .ToListAsync(ct);

        return new ContentEntryPage(items, total, options.Page, options.PageSize);
    }

    private static IQueryable<ContentEntry> ApplyFilter(
        IQueryable<ContentEntry> query,
        ContentField field,
        ContentFilter filter)
    {
        var operation = filter.Operator.ToLowerInvariant();

        return field.Type switch
        {
            ContentFieldType.Text => ApplyTextFilter(query, field.Key, operation, filter.Value),
            ContentFieldType.Number => ApplyNumberFilter(query, field.Key, operation, filter.Value),
            ContentFieldType.Boolean => ApplyBooleanFilter(query, field.Key, operation, filter.Value),
            _ => throw new ContentValidationException(
                $"Field '{field.Key}' has an unsupported filter type.")
        };
    }

    private static IQueryable<ContentEntry> ApplySystemFilter(
        IQueryable<ContentEntry> query,
        ContentFilter filter)
    {
        return filter.FieldKey switch
        {
            "$id" => ApplyIdFilter(query, filter),
            "$status" => ApplyStatusFilter(query, filter),
            "$createdAt" => ApplyTimestampFilter(query, filter, createdAt: true),
            "$updatedAt" => ApplyTimestampFilter(query, filter, createdAt: false),
            _ => throw new ContentValidationException(
                $"Field '{filter.FieldKey}' is not declared by the content type.")
        };
    }

    private static IQueryable<ContentEntry> ApplyIdFilter(
        IQueryable<ContentEntry> query,
        ContentFilter filter)
    {
        if (filter.Operator.ToLowerInvariant() != "eq" ||
            !Guid.TryParse(filter.Value, out var id))
        {
            throw new ContentValidationException(
                "$id supports only the eq operator with a valid UUID.");
        }

        return query.Where(entry => entry.Id == id);
    }

    private static IQueryable<ContentEntry> ApplyStatusFilter(
        IQueryable<ContentEntry> query,
        ContentFilter filter)
    {
        if (filter.Operator.ToLowerInvariant() != "eq" ||
            (!filter.Value.Equals("draft", StringComparison.OrdinalIgnoreCase) &&
             !filter.Value.Equals("published", StringComparison.OrdinalIgnoreCase)) ||
            !Enum.TryParse<ContentEntryStatus>(
                filter.Value,
                ignoreCase: true,
                out var status))
        {
            throw new ContentValidationException(
                "$status supports only the eq operator with draft or published.");
        }

        return query.Where(entry => entry.Status == status);
    }

    private static IQueryable<ContentEntry> ApplyTimestampFilter(
        IQueryable<ContentEntry> query,
        ContentFilter filter,
        bool createdAt)
    {
        if (!filter.Value.Contains('T') || !DateTimeOffset.TryParse(
                filter.Value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out var parsedTimestamp))
        {
            throw new ContentValidationException(
                $"Filter value '{filter.Value}' is not a valid ISO-8601 timestamp.");
        }

        var timestamp = parsedTimestamp.UtcDateTime;

        return (createdAt, filter.Operator.ToLowerInvariant()) switch
        {
            (true, "eq") => query.Where(entry => entry.CreatedAt == timestamp),
            (true, "gt") => query.Where(entry => entry.CreatedAt > timestamp),
            (true, "gte") => query.Where(entry => entry.CreatedAt >= timestamp),
            (true, "lt") => query.Where(entry => entry.CreatedAt < timestamp),
            (true, "lte") => query.Where(entry => entry.CreatedAt <= timestamp),
            (false, "eq") => query.Where(entry => entry.UpdatedAt == timestamp),
            (false, "gt") => query.Where(entry => entry.UpdatedAt > timestamp),
            (false, "gte") => query.Where(entry => entry.UpdatedAt >= timestamp),
            (false, "lt") => query.Where(entry => entry.UpdatedAt < timestamp),
            (false, "lte") => query.Where(entry => entry.UpdatedAt <= timestamp),
            _ => throw new ContentValidationException(
                "Timestamps support eq, gt, gte, lt, and lte operators.")
        };
    }

    private static IQueryable<ContentEntry> ApplyTextFilter(
        IQueryable<ContentEntry> query,
        string key,
        string operation,
        string value)
    {
        return operation switch
        {
            "eq" => query.Where(entry =>
                entry.Data.RootElement.GetProperty(key).GetString() == value),
            "contains" => query.Where(entry =>
                entry.Data.RootElement.GetProperty(key).GetString()!.Contains(value)),
            _ => throw new ContentValidationException(
                $"Operator '{operation}' is not supported for text fields.")
        };
    }

    private static IQueryable<ContentEntry> ApplyNumberFilter(
        IQueryable<ContentEntry> query,
        string key,
        string operation,
        string value)
    {
        if (!decimal.TryParse(
                value,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var number))
        {
            throw new ContentValidationException(
                $"Filter value '{value}' is not a valid number.");
        }

        return operation switch
        {
            "eq" => query.Where(entry =>
                entry.Data.RootElement.GetProperty(key).GetDecimal() == number),
            "gt" => query.Where(entry =>
                entry.Data.RootElement.GetProperty(key).GetDecimal() > number),
            "gte" => query.Where(entry =>
                entry.Data.RootElement.GetProperty(key).GetDecimal() >= number),
            "lt" => query.Where(entry =>
                entry.Data.RootElement.GetProperty(key).GetDecimal() < number),
            "lte" => query.Where(entry =>
                entry.Data.RootElement.GetProperty(key).GetDecimal() <= number),
            _ => throw new ContentValidationException(
                $"Operator '{operation}' is not supported for number fields.")
        };
    }

    private static IQueryable<ContentEntry> ApplyBooleanFilter(
        IQueryable<ContentEntry> query,
        string key,
        string operation,
        string value)
    {
        if (operation != "eq" || !bool.TryParse(value, out var boolean))
        {
            throw new ContentValidationException(
                "Boolean fields support only 'eq' with a true or false value.");
        }

        return query.Where(entry =>
            entry.Data.RootElement.GetProperty(key).GetBoolean() == boolean);
    }

    private static IQueryable<ContentEntry> ApplySort(
        IQueryable<ContentEntry> query,
        IReadOnlyDictionary<string, ContentField> fields,
        string? sort)
    {
        if (string.IsNullOrWhiteSpace(sort))
            return query.OrderByDescending(entry => entry.UpdatedAt).ThenBy(entry => entry.Id);

        var descending = sort.StartsWith('-');
        var fieldKey = descending ? sort[1..] : sort;

        var systemOrder = fieldKey switch
        {
            "$id" when descending => query.OrderByDescending(entry => entry.Id),
            "$id" => query.OrderBy(entry => entry.Id),
            "$status" when descending => query.OrderByDescending(entry => entry.Status),
            "$status" => query.OrderBy(entry => entry.Status),
            "$createdAt" when descending => query.OrderByDescending(entry => entry.CreatedAt),
            "$createdAt" => query.OrderBy(entry => entry.CreatedAt),
            "$updatedAt" when descending => query.OrderByDescending(entry => entry.UpdatedAt),
            "$updatedAt" => query.OrderBy(entry => entry.UpdatedAt),
            _ => null
        };

        if (systemOrder is not null)
            return systemOrder.ThenBy(entry => entry.Id);

        if (!fields.TryGetValue(fieldKey, out var field))
            throw new ContentValidationException(
                $"Sort field '{fieldKey}' is not declared by the content type.");

        IOrderedQueryable<ContentEntry> ordered = field.Type switch
        {
            ContentFieldType.Text when descending => query.OrderByDescending(entry =>
                entry.Data.RootElement.GetProperty(fieldKey).GetString()),
            ContentFieldType.Text => query.OrderBy(entry =>
                entry.Data.RootElement.GetProperty(fieldKey).GetString()),
            ContentFieldType.Number when descending => query.OrderByDescending(entry =>
                entry.Data.RootElement.GetProperty(fieldKey).GetDecimal()),
            ContentFieldType.Number => query.OrderBy(entry =>
                entry.Data.RootElement.GetProperty(fieldKey).GetDecimal()),
            ContentFieldType.Boolean when descending => query.OrderByDescending(entry =>
                entry.Data.RootElement.GetProperty(fieldKey).GetBoolean()),
            ContentFieldType.Boolean => query.OrderBy(entry =>
                entry.Data.RootElement.GetProperty(fieldKey).GetBoolean()),
            _ => throw new ContentValidationException(
                $"Sort field '{fieldKey}' has an unsupported type.")
        };

        return ordered.ThenBy(entry => entry.Id);
    }
}
