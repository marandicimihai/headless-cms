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
        Guid tenantId,
        Guid projectId,
        string contentTypeKey,
        JsonElement data,
        ContentEntryStatus status,
        CancellationToken ct = default)
    {
        var definition = await definitions.GetCurrentAsync(
            tenantId,
            projectId,
            contentTypeKey,
            ct);
        if (definition is null)
            return null;

        var validatedData = documentValidator.Validate(data, definition.Version.Fields);
        var now = DateTime.UtcNow;
        var entry = new ContentEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = projectId,
            ContentTypeId = definition.ContentType.Id,
            ContentTypeVersionId = definition.Version.Id,
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
        Guid tenantId,
        Guid projectId,
        string contentTypeKey,
        Guid entryId,
        CancellationToken ct = default)
    {
        return db.ContentEntries
            .AsNoTracking()
            .SingleOrDefaultAsync(
                entry =>
                    entry.TenantId == tenantId &&
                    entry.ProjectId == projectId &&
                    entry.ContentType.Key == contentTypeKey &&
                    entry.Id == entryId,
                ct);
    }

    public async Task<ContentEntry?> UpdateAsync(
        Guid tenantId,
        Guid projectId,
        string contentTypeKey,
        Guid entryId,
        JsonElement data,
        ContentEntryStatus status,
        CancellationToken ct = default)
    {
        var entry = await db.ContentEntries.SingleOrDefaultAsync(
            candidate =>
                candidate.TenantId == tenantId &&
                candidate.ProjectId == projectId &&
                candidate.ContentType.Key == contentTypeKey &&
                candidate.Id == entryId,
            ct);

        if (entry is null)
            return null;

        var fields = await db.ContentFields
            .AsNoTracking()
            .Where(field =>
                field.TenantId == tenantId &&
                field.ProjectId == projectId &&
                field.ContentTypeVersionId == entry.ContentTypeVersionId)
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
        Guid tenantId,
        Guid projectId,
        string contentTypeKey,
        Guid entryId,
        CancellationToken ct = default)
    {
        var entry = await db.ContentEntries.SingleOrDefaultAsync(
            candidate =>
                candidate.TenantId == tenantId &&
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
        Guid tenantId,
        Guid projectId,
        string contentTypeKey,
        ContentEntryQuery options,
        CancellationToken ct = default)
    {
        var definition = await definitions.GetCurrentAsync(
            tenantId,
            projectId,
            contentTypeKey,
            ct);
        if (definition is null)
            return null;

        var fields = definition.Version.Fields.ToDictionary(
            field => field.Key,
            StringComparer.Ordinal);

        IQueryable<ContentEntry> query = db.ContentEntries
            .AsNoTracking()
            .Where(entry =>
                entry.TenantId == tenantId &&
                entry.ProjectId == projectId &&
                entry.ContentTypeId == definition.ContentType.Id);

        if (options.Status.HasValue)
            query = query.Where(entry => entry.Status == options.Status.Value);

        foreach (var filter in options.Filters)
        {
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
            return query.OrderByDescending(entry => entry.CreatedAt).ThenBy(entry => entry.Id);

        var descending = sort.StartsWith('-');
        var fieldKey = descending ? sort[1..] : sort;

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
