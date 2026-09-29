using FluentValidation;
using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Content.Services;
using HeadlessCms.Api.Workspaces.Services;

using HeadlessCms.Api.Documentation;

namespace HeadlessCms.Api.Endpoints.Search;

public sealed class SearchWorkspace(
    WorkspaceSearchService search,
    WorkspaceAccessService workspaceAccess)
    : Endpoint<SearchWorkspaceRequest, SearchWorkspaceResponse>
{
    public override void Configure()
    {
        Get("workspaces/{workspaceId:guid}/search");
        Claims("sub");
        Description(b => b.WithTags("Search"), clearDefaults: true);
        Summary((EndpointSummary s) =>
        {
            s.Summary = "Search a workspace";
            s.Description = "Access: Workspace Owner, Editor, or Member.\n\nSearches project names, content-type keys, and text entry fields. Query must be 2–100 characters after trimming; limit is 1–10 results per group (default 5). Each group includes its total match count.";
            s.Params["WorkspaceId"] = "Workspace UUID.";
            s.Params["Query"] = "Search text, 2–100 characters after trimming.";
            s.Params["Limit"] = "Maximum results per group, 1–10 (default 5).";
            s.Response<SearchWorkspaceResponse>(200, "Success.");
            s.ResponseExamples[200] = new { Projects = new { Items = new[] { new { Id = ApiExamples.ProjectId, Name = "Website" } }, Total = 1 }, ContentTypes = new { Items = new[] { new { Id = ApiExamples.ContentTypeId, Key = "articles", ProjectId = ApiExamples.ProjectId, ProjectName = "Website" } }, Total = 1 }, Entries = new { Items = new[] { new { Id = ApiExamples.EntryId, Status = "draft", ContentTypeKey = "articles", ProjectId = ApiExamples.ProjectId, ProjectName = "Website", MatchedFieldKey = "title", Snippet = "Hello world" } }, Total = 1 } };
            s.Response<FastEndpoints.ProblemDetails>(400, "Invalid request or validation failure.", "application/problem+json");
            s.Response<ApiProblem>(401, "Authentication is required, or the session has expired.", "application/problem+json");
            s.Response(404, "Resource not found or inaccessible.");
        });
    }

    public override async Task HandleAsync(SearchWorkspaceRequest request, CancellationToken ct)
    {
        if (await workspaceAccess.ResolveAsync(
                User,
                request.WorkspaceId,
                WorkspaceAccessRoles.Members,
                ct) is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var result = await search.SearchAsync(
            request.WorkspaceId,
            request.Query.Trim(),
            request.Limit,
            ct);

        Response = new SearchWorkspaceResponse(
            new SearchWorkspaceGroup<SearchWorkspaceProjectResponse>(
                result.Projects.Items
                    .Select(item => new SearchWorkspaceProjectResponse(item.Id, item.Name))
                    .ToList(),
                result.Projects.Total),
            new SearchWorkspaceGroup<SearchWorkspaceContentTypeResponse>(
                result.ContentTypes.Items
                    .Select(item => new SearchWorkspaceContentTypeResponse(
                        item.Id,
                        item.Key,
                        item.ProjectId,
                        item.ProjectName))
                    .ToList(),
                result.ContentTypes.Total),
            new SearchWorkspaceGroup<SearchWorkspaceEntryResponse>(
                result.Entries.Items
                    .Select(item => new SearchWorkspaceEntryResponse(
                        item.Id,
                        item.Status,
                        item.ContentTypeKey,
                        item.ProjectId,
                        item.ProjectName,
                        item.MatchedFieldKey,
                        item.Snippet))
                    .ToList(),
                result.Entries.Total));
    }
}

public sealed class SearchWorkspaceRequest
{
    public Guid WorkspaceId { get; init; }
    [FastEndpoints.QueryParam]
    public string Query { get; init; } = string.Empty;
    [System.ComponentModel.DefaultValue(5)]
    [FastEndpoints.QueryParam]
    public int Limit { get; init; } = 5;
}

public sealed class SearchWorkspaceRequestValidator : Validator<SearchWorkspaceRequest>
{
    public SearchWorkspaceRequestValidator()
    {
        RuleFor(request => request.Query)
            .Must(query => query is not null && query.Trim().Length is >= 2 and <= 100)
            .WithMessage("Query must contain between 2 and 100 non-whitespace characters.");
        RuleFor(request => request.Limit).InclusiveBetween(1, 10);
    }
}

public sealed record SearchWorkspaceResponse(
    SearchWorkspaceGroup<SearchWorkspaceProjectResponse> Projects,
    SearchWorkspaceGroup<SearchWorkspaceContentTypeResponse> ContentTypes,
    SearchWorkspaceGroup<SearchWorkspaceEntryResponse> Entries);

public sealed record SearchWorkspaceGroup<T>(IReadOnlyList<T> Items, int Total);

public sealed record SearchWorkspaceProjectResponse(Guid Id, string Name);

public sealed record SearchWorkspaceContentTypeResponse(
    Guid Id,
    string Key,
    Guid ProjectId,
    string ProjectName);

public sealed record SearchWorkspaceEntryResponse(
    Guid Id,
    ContentEntryStatus Status,
    string ContentTypeKey,
    Guid ProjectId,
    string ProjectName,
    string MatchedFieldKey,
    string Snippet);
