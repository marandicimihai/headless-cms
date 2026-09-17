using System.Text.Json;
using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Content.Services;

namespace HeadlessCms.Api.Content.FieldTypes;

public sealed class TextFieldTypeHandler : IContentFieldTypeHandler
{
    public ContentFieldType Type => ContentFieldType.Text;
    public bool SupportsTextSearch => true;

    public void ValidateValue(ContentField field, JsonElement value, ICollection<string> errors)
    {
        if (value.ValueKind != JsonValueKind.String)
            errors.Add($"Field '{field.Key}' must contain a text value.");
    }

    public IQueryable<ContentEntry> ApplyFilter(
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

    public IOrderedQueryable<ContentEntry> ApplySort(
        IQueryable<ContentEntry> query, string key, bool descending) =>
        descending
            ? query.OrderByDescending(entry => entry.Data.RootElement.GetProperty(key).GetString())
            : query.OrderBy(entry => entry.Data.RootElement.GetProperty(key).GetString());
}
