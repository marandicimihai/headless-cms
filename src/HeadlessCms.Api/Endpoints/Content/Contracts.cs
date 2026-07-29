using System.Text.Json;
using System.Text.RegularExpressions;
using FluentValidation;
using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Content.Services;
using HeadlessCms.Api.Tenancy.Models;

namespace HeadlessCms.Api.Endpoints.Content;

public sealed class ContentFieldRequest
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required string Type { get; init; }
    public bool Required { get; init; }
    public bool Nullable { get; init; }
    public JsonElement Settings { get; init; }
}

public sealed class CreateContentTypeRequest
{
    public Guid TenantId { get; init; }
    public Guid ProjectId { get; init; }
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required List<ContentFieldRequest> Fields { get; init; }
}

public sealed class UpdateContentTypeRequest
{
    public Guid TenantId { get; init; }
    public Guid ProjectId { get; init; }
    public string ContentTypeKey { get; init; } = default!;
    public required string Name { get; init; }
    public required List<ContentFieldRequest> Fields { get; init; }
}

public sealed class ContentTypeKeyRequest
{
    public Guid TenantId { get; init; }
    public Guid ProjectId { get; init; }
    public string ContentTypeKey { get; init; } = default!;
}

public sealed class ProjectContentRequest
{
    public Guid TenantId { get; init; }
    public Guid ProjectId { get; init; }
}

public sealed class ContentFieldResponse
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required string Type { get; init; }
    public bool Required { get; init; }
    public bool Nullable { get; init; }
    public int Position { get; init; }
    public JsonElement Settings { get; init; }
}

public sealed class ContentTypeResponse
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public required string Key { get; init; }
    public required string Name { get; init; }
    public int Version { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public required IReadOnlyList<ContentFieldResponse> Fields { get; init; }
}

public sealed class CreateContentEntryRequest
{
    public Guid TenantId { get; init; }
    public Guid ProjectId { get; init; }
    public string ContentTypeKey { get; init; } = default!;
    public JsonElement Data { get; init; }
    public string Status { get; init; } = "draft";
}

public sealed class UpdateContentEntryRequest
{
    public Guid TenantId { get; init; }
    public Guid ProjectId { get; init; }
    public string ContentTypeKey { get; init; } = default!;
    public Guid EntryId { get; init; }
    public JsonElement Data { get; init; }
    public string Status { get; init; } = "draft";
}

public sealed class ContentEntryIdRequest
{
    public Guid TenantId { get; init; }
    public Guid ProjectId { get; init; }
    public string ContentTypeKey { get; init; } = default!;
    public Guid EntryId { get; init; }
}

public sealed class ListContentEntriesRequest
{
    public Guid TenantId { get; init; }
    public Guid ProjectId { get; init; }
    public string ContentTypeKey { get; init; } = default!;
    public string? Sort { get; init; }
    public string? Status { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
}

public sealed class ContentEntryResponse
{
    public Guid Id { get; init; }
    public int SchemaVersion { get; init; }
    public required string Status { get; init; }
    public JsonElement Data { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed class ContentEntryPageResponse
{
    public required IReadOnlyList<ContentEntryResponse> Items { get; init; }
    public int Total { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

public sealed class CreateContentTypeRequestValidator : Validator<CreateContentTypeRequest>
{
    public CreateContentTypeRequestValidator()
    {
        RuleFor(request => request.Key)
            .NotEmpty()
            .MaximumLength(64)
            .Matches(ContentContract.KeyPattern);
        RuleFor(request => request.Name)
            .NotEmpty()
            .Must(name => name.Trim().Length is >= 3 and <= 100)
            .WithMessage("Name must contain between 3 and 100 characters.");
        RuleFor(request => request.Fields).NotEmpty();
        RuleForEach(request => request.Fields).SetValidator(new ContentFieldRequestValidator());
    }
}

public sealed class UpdateContentTypeRequestValidator : Validator<UpdateContentTypeRequest>
{
    public UpdateContentTypeRequestValidator()
    {
        RuleFor(request => request.ContentTypeKey)
            .NotEmpty()
            .MaximumLength(64)
            .Matches(ContentContract.KeyPattern);
        RuleFor(request => request.Name)
            .NotEmpty()
            .Must(name => name.Trim().Length is >= 3 and <= 100)
            .WithMessage("Name must contain between 3 and 100 characters.");
        RuleFor(request => request.Fields).NotEmpty();
        RuleForEach(request => request.Fields).SetValidator(new ContentFieldRequestValidator());
    }
}

public sealed class ContentFieldRequestValidator : Validator<ContentFieldRequest>
{
    public ContentFieldRequestValidator()
    {
        RuleFor(request => request.Key)
            .NotEmpty()
            .MaximumLength(64)
            .Matches(ContentContract.KeyPattern);
        RuleFor(request => request.Name)
            .NotEmpty()
            .Must(name => name.Trim().Length is >= 1 and <= 100)
            .WithMessage("Field name must contain between 1 and 100 characters.");
        RuleFor(request => request.Type)
            .Must(ContentContract.TryParseFieldType)
            .WithMessage("Type must be text, number, or boolean.");
        RuleFor(request => request.Settings)
            .Must(settings =>
                settings.ValueKind is JsonValueKind.Undefined or
                    JsonValueKind.Null or
                    JsonValueKind.Object)
            .WithMessage("Settings must be a JSON object.");
    }
}

public sealed class ContentEntryRequestValidator : Validator<CreateContentEntryRequest>
{
    public ContentEntryRequestValidator()
    {
        RuleFor(request => request.Data)
            .Must(data => data.ValueKind == JsonValueKind.Object)
            .WithMessage("Data must be a JSON object.");
        RuleFor(request => request.Status)
            .Must(ContentContract.TryParseStatus)
            .WithMessage("Status must be draft or published.");
    }
}

public sealed class UpdateContentEntryRequestValidator : Validator<UpdateContentEntryRequest>
{
    public UpdateContentEntryRequestValidator()
    {
        RuleFor(request => request.Data)
            .Must(data => data.ValueKind == JsonValueKind.Object)
            .WithMessage("Data must be a JSON object.");
        RuleFor(request => request.Status)
            .Must(ContentContract.TryParseStatus)
            .WithMessage("Status must be draft or published.");
    }
}

public sealed class ListContentEntriesRequestValidator : Validator<ListContentEntriesRequest>
{
    public ListContentEntriesRequestValidator()
    {
        RuleFor(request => request.Page).GreaterThan(0);
        RuleFor(request => request.PageSize).InclusiveBetween(1, 100);
        RuleFor(request => request.Status)
            .Must(status => status is null || ContentContract.TryParseStatus(status))
            .WithMessage("Status must be draft or published.");
    }
}

internal static class ContentContract
{
    public const string KeyPattern = "^[a-z][a-z0-9_]*$";

    public static readonly IReadOnlySet<TenantRole> Readers =
        new HashSet<TenantRole>([TenantRole.Owner, TenantRole.Editor, TenantRole.Member]);

    public static readonly IReadOnlySet<TenantRole> Writers =
        new HashSet<TenantRole>([TenantRole.Owner, TenantRole.Editor]);

    private static readonly Regex FilterPattern = new(
        @"^filter\[(?<field>[a-z][a-z0-9_]*)\]\[(?<operator>[a-z]+)\]$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool TryParseFieldType(string value) =>
        Enum.TryParse<ContentFieldType>(value, true, out _);

    public static ContentFieldType ParseFieldType(string value) =>
        Enum.Parse<ContentFieldType>(value, true);

    public static bool TryParseStatus(string value) =>
        Enum.TryParse<ContentEntryStatus>(value, true, out _);

    public static ContentEntryStatus ParseStatus(string value) =>
        Enum.Parse<ContentEntryStatus>(value, true);

    public static IReadOnlyList<ContentFieldInput> ToInputs(
        IEnumerable<ContentFieldRequest> fields) =>
        fields.Select(field => new ContentFieldInput(
                field.Key,
                field.Name.Trim(),
                ParseFieldType(field.Type),
                field.Required,
                field.Nullable,
                field.Settings))
            .ToList();

    public static ContentTypeResponse ToResponse(ContentTypeDefinition definition) =>
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
                .Select(field => new ContentFieldResponse
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

    public static ContentEntryResponse ToResponse(
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

    public static IReadOnlyList<ContentFilter> ParseFilters(
        IQueryCollection query)
    {
        var filters = new List<ContentFilter>();

        foreach (var (key, values) in query)
        {
            if (!key.StartsWith("filter[", StringComparison.Ordinal))
                continue;

            var match = FilterPattern.Match(key);
            if (!match.Success)
            {
                throw new ContentValidationException(
                    $"Filter '{key}' is malformed. Use filter[field][operator].");
            }

            foreach (var value in values)
            {
                filters.Add(new ContentFilter(
                    match.Groups["field"].Value,
                    match.Groups["operator"].Value,
                    value ?? string.Empty));
            }
        }

        return filters;
    }
}
