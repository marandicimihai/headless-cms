using System.Globalization;
using System.Text.Json;
using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Content.Services;

namespace HeadlessCms.Api.Content.FieldTypes;

public sealed class NumberFieldTypeHandler : IContentFieldTypeHandler
{
    public ContentFieldType Type => ContentFieldType.Number;
    public bool SupportsTextSearch => false;

    public void ValidateValue(ContentField field, JsonElement value, ICollection<string> errors)
    {
        if (!(value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out _)))
            errors.Add($"Field '{field.Key}' must contain a number value.");
    }

    public IQueryable<ContentEntry> ApplyFilter(
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

    public IOrderedQueryable<ContentEntry> ApplySort(
        IQueryable<ContentEntry> query, string key, bool descending) =>
        descending
            ? query.OrderByDescending(entry => entry.Data.RootElement.GetProperty(key).GetDecimal())
            : query.OrderBy(entry => entry.Data.RootElement.GetProperty(key).GetDecimal());
}
