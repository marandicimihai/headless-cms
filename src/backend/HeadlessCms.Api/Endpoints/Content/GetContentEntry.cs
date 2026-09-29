using System.Text.Json;
using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Content.Services;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;

using HeadlessCms.Api.Documentation;

namespace HeadlessCms.Api.Endpoints.Content;

public sealed class GetContentEntry(
    ContentEntryService entries,
    WorkspaceAccessService workspaceAccess)
    : Endpoint<GetContentEntryRequest, GetContentEntryResponse>
{
    public override void Configure()
    {
        Get(
            "workspaces/{workspaceId:guid}/projects/{projectId:guid}/" +
            "content-types/{contentTypeKey}/entries/{entryId:guid}");
        Claims("sub");
        Description(b => b.WithTags("Content entries"), clearDefaults: true);
        Summary((EndpointSummary s) =>
        {
            s.Summary = "Get an entry";
            s.Description = "Access: Workspace Owner, Editor, or Member.\n\nReturns the entry, including its status and data. Draft entries are accessible to all workspace members.";
            s.Params["WorkspaceId"] = "Workspace UUID.";
            s.Params["ProjectId"] = "Project UUID within the workspace.";
            s.Params["ContentTypeKey"] = "Immutable content-type key, for example articles.";
            s.Params["EntryId"] = "Entry UUID within the content type.";
            s.Response<GetContentEntryResponse>(200, "Success.");
            s.ResponseExamples[200] = ApiExamples.Entry;
            s.Response<FastEndpoints.ProblemDetails>(400, "Invalid request or validation failure.", "application/problem+json");
            s.Response<ApiProblem>(401, "Authentication is required, or the session has expired.", "application/problem+json");
            s.Response(403, "The caller does not have permission.");
            s.Response(404, "Resource not found or inaccessible.");
        });
    }

    public override async Task HandleAsync(GetContentEntryRequest request, CancellationToken ct)
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

        var entry = await entries.GetAsync(
            request.WorkspaceId,
            request.ProjectId,
            request.ContentTypeKey,
            request.EntryId,
            ct);

        if (entry is null)
            await Send.NotFoundAsync(ct);
        else
        {
            Response = ToResponse(entry);
            entry.Dispose();
        }
    }

    private static GetContentEntryResponse ToResponse(ContentEntry entry) =>
        new()
        {
            Id = entry.Id,
            Status = entry.Status,
            Data = entry.Data.RootElement.Clone(),
            CreatedAt = entry.CreatedAt,
            UpdatedAt = entry.UpdatedAt
        };
}

public sealed class GetContentEntryRequest
{
    public Guid WorkspaceId { get; init; }
    public Guid ProjectId { get; init; }
    public string ContentTypeKey { get; init; } = default!;
    public Guid EntryId { get; init; }
}

public sealed class GetContentEntryResponse
{
    public Guid Id { get; init; }
    public required ContentEntryStatus Status { get; init; }
    public JsonElement Data { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
