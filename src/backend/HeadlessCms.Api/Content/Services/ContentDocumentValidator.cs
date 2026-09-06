using System.Text.Json;
using System.Text.Json.Nodes;
using HeadlessCms.Api.Content.Models;

namespace HeadlessCms.Api.Content.Services;

public class ContentDocumentValidator
{
    public JsonDocument Validate(
        JsonElement data,
        IReadOnlyCollection<ContentField> fields)
    {
        if (data.ValueKind != JsonValueKind.Object)
            throw new ContentValidationException("Data must be a JSON object.");

        var fieldsByKey = fields.ToDictionary(field => field.Key, StringComparer.Ordinal);
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        var populatedKeys = new HashSet<string>(StringComparer.Ordinal);
        var errors = new List<string>();
        var result = new JsonObject();

        foreach (var property in data.EnumerateObject())
        {
            if (!seenKeys.Add(property.Name))
            {
                errors.Add($"Field '{property.Name}' occurs more than once.");
                continue;
            }

            if (!fieldsByKey.TryGetValue(property.Name, out var field))
            {
                errors.Add($"Field '{property.Name}' is not declared by the content type.");
                continue;
            }

            if (IsMissingOptionalValue(field, property.Value))
                continue;

            ValidateValue(field, property.Value, errors);
            populatedKeys.Add(field.Key);
            result[field.Key] = JsonNode.Parse(property.Value.GetRawText());
        }

        foreach (var field in fields.OrderBy(candidate => candidate.Position))
        {
            if (populatedKeys.Contains(field.Key))
                continue;

            if (TryGetDefault(field.Settings, out var defaultValue))
            {
                ValidateValue(field, defaultValue, errors);
                result[field.Key] = JsonNode.Parse(defaultValue.GetRawText());
                continue;
            }

            if (field.Required)
                errors.Add($"Field '{field.Key}' is required.");
        }

        if (errors.Count > 0)
            throw new ContentValidationException(errors);

        return JsonDocument.Parse(result.ToJsonString());
    }

    public void ValidateFieldSettings(
        ContentFieldType type,
        string fieldKey,
        bool required,
        JsonElement settings)
    {
        if (settings.ValueKind != JsonValueKind.Object)
            throw new ContentValidationException(
                $"Settings for field '{fieldKey}' must be a JSON object.");

        if (!TryGetDefault(settings, out var defaultValue))
            return;

        if (required)
            throw new ContentValidationException(
                $"Required field '{fieldKey}' cannot define a default value.");

        var field = new ContentField
        {
            Key = fieldKey,
            Type = type,
        };
        var errors = new List<string>();
        ValidateValue(field, defaultValue, errors);

        if (errors.Count > 0)
            throw new ContentValidationException(errors);
    }

    private static bool TryGetDefault(JsonElement settings, out JsonElement defaultValue)
    {
        if (settings.ValueKind == JsonValueKind.Object &&
            settings.TryGetProperty("default", out defaultValue))
        {
            return true;
        }

        defaultValue = default;
        return false;
    }

    private static bool IsMissingOptionalValue(ContentField field, JsonElement value) =>
        !field.Required && value.ValueKind == JsonValueKind.Null;

    private static void ValidateValue(
        ContentField field,
        JsonElement value,
        ICollection<string> errors)
    {
        var valid = field.Type switch
        {
            ContentFieldType.Text => value.ValueKind == JsonValueKind.String,
            ContentFieldType.Number =>
                value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out _),
            ContentFieldType.Boolean =>
                value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            _ => false
        };

        if (!valid)
        {
            errors.Add(
                $"Field '{field.Key}' must contain a {field.Type.ToString().ToLowerInvariant()} value.");
        }
    }
}
