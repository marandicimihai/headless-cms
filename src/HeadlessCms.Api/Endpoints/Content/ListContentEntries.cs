using System.Text.Json;
using System.Text.RegularExpressions;
using FluentValidation;
using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Content.Services;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Tenancy.Models;
using HeadlessCms.Api.Tenancy.Services;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Endpoints.Content;

public sealed class ListContentEntries(
    ContentEntryService entries,
    ApplicationDbContext db,
    TenantAccessService tenantAccess)
    : Endpoint<ListContentEntriesRequest, ListContentEntriesResponse>
{
    private static readonly IReadOnlySet<TenantRole> Readers =
        new HashSet<TenantRole>([TenantRole.Owner, TenantRole.Editor, TenantRole.Member]);

    private static readonly Regex FilterPattern = new(
        @"^filter\[(?<field>[a-z][a-z0-9_]*)\]\[(?<operator>[a-z]+)\]$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public override void Configure()
    {
        Get(
            "tenants/{tenantId:guid}/projects/{projectId:guid}/" +
            "content-types/{contentTypeKey}/entries");
        Claims("sub");
    }

    public override async Task HandleAsync(ListContentEntriesRequest request, CancellationToken ct)
    {
        if (await tenantAccess.ResolveAsync(User, request.TenantId, Readers, ct) is null)
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        try
        {
            var page = await entries.QueryAsync(
                request.TenantId,
                request.ProjectId,
                request.ContentTypeKey,
                new ContentEntryQuery(
                    ParseFilters(HttpContext.Request.Query),
                    request.Sort,
                    request.Status is null ? null : ParseStatus(request.Status),
                    request.Page,
                    request.PageSize),
                ct);

            if (page is null)
            {
                await Send.NotFoundAsync(ct);
                return;
            }

            var versionIds = page.Items
                .Select(entry => entry.ContentTypeVersionId)
                .Distinct()
                .ToList();
            var versions = await db.ContentTypeVersions
                .Where(version =>
                    version.TenantId == request.TenantId &&
                    version.ProjectId == request.ProjectId &&
                    versionIds.Contains(version.Id))
                .ToDictionaryAsync(version => version.Id, version => version.Version, ct);

            Response = new ListContentEntriesResponse
            {
                Items = page.Items
                    .Select(entry =>
                        ToResponse(entry, versions[entry.ContentTypeVersionId]))
                    .ToList(),
                Total = page.Total,
                Page = page.Page,
                PageSize = page.PageSize
            };

            foreach (var entry in page.Items)
                entry.Dispose();
        }
        catch (ContentValidationException exception)
        {
            foreach (var error in exception.Errors)
                AddError(error);
            await Send.ErrorsAsync(cancellation: ct);
        }
    }

    internal static bool TryParseStatus(string value) =>
        Enum.TryParse<ContentEntryStatus>(value, true, out _);

    private static ContentEntryStatus ParseStatus(string value) =>
        Enum.Parse<ContentEntryStatus>(value, true);

    private static ListContentEntriesItemResponse ToResponse(
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

    private static IReadOnlyList<ContentFilter> ParseFilters(IQueryCollection query)
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

public sealed class ListContentEntriesResponse
{
    public required IReadOnlyList<ListContentEntriesItemResponse> Items { get; init; }
    public int Total { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

public sealed class ListContentEntriesItemResponse
{
    public Guid Id { get; init; }
    public int SchemaVersion { get; init; }
    public required string Status { get; init; }
    public JsonElement Data { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed class ListContentEntriesRequestValidator : Validator<ListContentEntriesRequest>
{
    public ListContentEntriesRequestValidator()
    {
        RuleFor(request => request.Page).GreaterThan(0);
        RuleFor(request => request.PageSize).InclusiveBetween(1, 100);
        RuleFor(request => request.Status)
            .Must(status => status is null || ListContentEntries.TryParseStatus(status))
            .WithMessage("Status must be draft or published.");
    }
}
