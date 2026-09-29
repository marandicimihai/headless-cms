using System.Text.Json;
using System.Text.RegularExpressions;
using FluentValidation;
using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Content.Services;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;

using HeadlessCms.Api.Documentation;

namespace HeadlessCms.Api.Endpoints.Content;

public sealed class ListContentEntries(
    ContentEntryService entries,
    WorkspaceAccessService workspaceAccess)
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
        Description(b => b.WithTags("Content entries"), clearDefaults: true);
        Summary((EndpointSummary s) =>
        {
            s.Summary = "List entries";
            s.Description = "Access: Workspace Owner, Editor, or Member.\n\nPaginated entries; page defaults to 1, pageSize to 25 (1–100). Both drafts and published entries are returned unless status is specified. Filters use filter[field][operator] and combine with AND. Text supports eq/contains; number eq/gt/gte/lt/lte; boolean eq. System fields: $id and $status support eq, $createdAt and $updatedAt support eq/gt/gte/lt/lte with ISO-8601 timestamps. Sort by one field or system field, prefix - for descending; default is -$updatedAt with ID as tie-breaker.";
            s.Params["WorkspaceId"] = "Workspace UUID.";
            s.Params["ProjectId"] = "Project UUID within the workspace.";
            s.Params["ContentTypeKey"] = "Immutable content-type key, for example articles.";
            s.Params["Sort"] = "One field or system field; prefix - for descending. Defaults to -$updatedAt.";
            s.Params["Status"] = "Optional status filter.";
            s.Params["Page"] = "Page number, starting at 1 (default 1).";
            s.Params["PageSize"] = "Items per page (maximum 100). Default 25; values outside 1–100 are rejected.";
            s.Response<ListContentEntriesResponse>(200, "Success.");
            s.ResponseExamples[200] = ApiExamples.Page(ApiExamples.Entry, 25);
            s.Response<FastEndpoints.ProblemDetails>(400, "Invalid request or validation failure.", "application/problem+json");
            s.Response<ApiProblem>(401, "Authentication is required, or the session has expired.", "application/problem+json");
            s.Response(403, "The caller does not have permission.");
            s.Response(404, "Resource not found or inaccessible.");
        });
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
                await Send.NotFoundAsync(ct);
            else
            {
                Response = new ListContentEntriesResponse
                {
                    Items = page.Items.Select(ToResponse).ToList(),
                    Total = page.Total,
                    Page = page.Page,
                    PageSize = page.PageSize
                };

                foreach (var entry in page.Items)
                    entry.Dispose();
            }
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
    [System.ComponentModel.DefaultValue("-$updatedAt")]
    [FastEndpoints.QueryParam]
    public string? Sort { get; init; }
    [FastEndpoints.QueryParam]
    public ContentEntryStatus? Status { get; init; }
    [System.ComponentModel.DefaultValue(1)]
    [FastEndpoints.QueryParam]
    public int Page { get; init; } = 1;
    [System.ComponentModel.DefaultValue(25)]
    [FastEndpoints.QueryParam]
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
