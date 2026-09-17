using System.Text.Json;
using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Content.Services;

namespace HeadlessCms.Api.Content.FieldTypes;

public sealed class BooleanFieldTypeHandler : IContentFieldTypeHandler
{
    public ContentFieldType Type => ContentFieldType.Boolean;
    public bool SupportsTextSearch => false;

    public void ValidateValue(ContentField field, JsonElement value, ICollection<string> errors)
    {
        if (!(value.ValueKind is JsonValueKind.True or JsonValueKind.False))
            errors.Add($"Field '{field.Key}' must contain a boolean value.");
    }

    public IQueryable<ContentEntry> ApplyFilter(
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

    public IOrderedQueryable<ContentEntry> ApplySort(
        IQueryable<ContentEntry> query, string key, bool descending) =>
        descending
            ? query.OrderByDescending(entry => entry.Data.RootElement.GetProperty(key).GetBoolean())
            : query.OrderBy(entry => entry.Data.RootElement.GetProperty(key).GetBoolean());
}
