using HeadlessCms.Api.Caching;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentValidation;
using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Content.Services;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;

namespace HeadlessCms.Api.Endpoints.Content;

public sealed class ListContentEntries(
    ContentEntryService entries,
    WorkspaceAccessService workspaceAccess,
    ResourceCache cache)
    : Endpoint<ListContentEntriesRequest, ListContentEntriesResponse>
{
    private static readonly Regex FilterPattern = new(
        @"^filter\[(?<field>\$[a-z][a-zA-Z]*|[a-z][a-z0-9_]*)\]\[(?<operator>[a-z]+)\]$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public override void Configure()
    {
        Get(
            "workspaces/{workspaceId:guid}/projects/{projectId:guid}/" +
            "content-types/{contentTypeKey}/entries");
        Claims("sub");
    }

    public override async Task HandleAsync(ListContentEntriesRequest request, CancellationToken ct)
    {
        if (await workspaceAccess.ResolveAsync(
                User,
                request.WorkspaceId,
                WorkspaceAccessRoles.Members,
                ct) is null)
        {
            await Send.ForbiddenAsync(ct);
            return;
        }

        try
        {
            var filters = ParseFilters(HttpContext.Request.Query);
            var response = await cache.GetOrLoadAsync<ListContentEntriesResponse>(
                request.WorkspaceId, ResourceCache.RequestKey(HttpContext.Request, nameof(ListContentEntries)),
                async ct =>
                {
                    var page = await entries.QueryAsync(
                        request.WorkspaceId,
                        request.ProjectId,
                        request.ContentTypeKey,
                        new ContentEntryQuery(
                            filters,
                            request.Sort,
                            request.Status,
                            request.Page,
                            request.PageSize),
                        ct);

                    if (page is null)
                    {
                        return null;
                    }

                    var result = new ListContentEntriesResponse
                    {
                        Items = page.Items
                            .Select(ToResponse)
                            .ToList(),
                        Total = page.Total,
                        Page = page.Page,
                        PageSize = page.PageSize
                    };

                    foreach (var entry in page.Items)
                        entry.Dispose();
                    return result;
                }, ct);
            if (response is null)
                await Send.NotFoundAsync(ct);
            else
                Response = response;
        }
        catch (ContentValidationException exception)
        {
            foreach (var error in exception.Errors)
                AddError(error);
            await Send.ErrorsAsync(cancellation: ct);
        }
    }

    private static ListContentEntriesItemResponse ToResponse(ContentEntry entry) =>
        new()
        {
            Id = entry.Id,
            Status = entry.Status,
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
    public Guid WorkspaceId { get; init; }
    public Guid ProjectId { get; init; }
    public string ContentTypeKey { get; init; } = default!;
    public string? Sort { get; init; }
    public ContentEntryStatus? Status { get; init; }
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
    public required ContentEntryStatus Status { get; init; }
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
            .IsInEnum()
            .When(request => request.Status.HasValue)
            .WithMessage("Status must be draft or published.");
    }
}
