using FastEndpoints;
using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;
using Microsoft.EntityFrameworkCore;

using HeadlessCms.Api.Documentation;

namespace HeadlessCms.Api.Endpoints.Projects;

public sealed class GetProjectPreviewRequest
{
    public Guid WorkspaceId { get; init; }
    public Guid Id { get; init; }
}

public sealed record ProjectPreviewEntry(Guid Id, string ContentTypeKey, ContentEntryStatus Status, DateTime UpdatedAt);
public sealed record GetProjectPreviewResponse(
    string Name, WorkspaceRole CurrentRole, int ContentTypeCount,
    int PublishedEntryCount, int DraftEntryCount, List<ProjectPreviewEntry> RecentEntries);

public sealed class GetProjectPreview(ApplicationDbContext db, WorkspaceAccessService workspaceAccess)
    : Endpoint<GetProjectPreviewRequest, GetProjectPreviewResponse>
{
    public override void Configure()
    {
        Get("workspaces/{workspaceId:guid}/projects/{id:guid}/preview");
        Claims("sub");
        Description(b => b.WithTags("Projects"), clearDefaults: true);
        Summary((EndpointSummary s) =>
        {
            s.Summary = "Get project overview";
            s.Description = "Access: Workspace Owner, Editor, or Member.\n\nReturns content-type and entry counts plus recent entries, including drafts.";
            s.Params["WorkspaceId"] = "Workspace UUID.";
            s.Params["Id"] = "Project UUID within the workspace.";
            s.Response<GetProjectPreviewResponse>(200, "Success.");
            s.ResponseExamples[200] = new { Name = "Website", CurrentRole = "editor", ContentTypeCount = 1, PublishedEntryCount = 0, DraftEntryCount = 1, RecentEntries = new[] { new { Id = ApiExamples.EntryId, ContentTypeKey = "articles", Status = "draft", UpdatedAt = ApiExamples.Timestamp } } };
            s.Response<FastEndpoints.ProblemDetails>(400, "Invalid request or validation failure.", "application/problem+json");
            s.Response<ApiProblem>(401, "Authentication is required, or the session has expired.", "application/problem+json");
            s.Response(404, "Resource not found or inaccessible.");
        });
    }

    public override async Task HandleAsync(GetProjectPreviewRequest request, CancellationToken ct)
    {
        var access = await workspaceAccess.ResolveAsync(User, request.WorkspaceId, WorkspaceAccessRoles.Members, ct);
        if (access is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        var project = await db.Projects.AsNoTracking().SingleOrDefaultAsync(
            p => p.Id == request.Id && p.WorkspaceId == request.WorkspaceId, ct);
        if (project is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var typeCount = await db.ContentTypes.CountAsync(
            t => t.WorkspaceId == request.WorkspaceId && t.ProjectId == request.Id, ct);
        var entries = db.ContentEntries.AsNoTracking()
            .Where(e => e.WorkspaceId == request.WorkspaceId && e.ProjectId == request.Id);
        var counts = await entries.GroupBy(e => 1)
            .Select(group => new
            {
                Published = group.Count(e => e.Status == ContentEntryStatus.Published),
                Drafts = group.Count(e => e.Status == ContentEntryStatus.Draft)
            })
            .SingleOrDefaultAsync(ct);
        var published = counts?.Published ?? 0;
        var drafts = counts?.Drafts ?? 0;
        var recent = await entries.OrderByDescending(e => e.UpdatedAt).ThenBy(e => e.Id)
            .Take(10).Select(e => new ProjectPreviewEntry(e.Id, e.ContentType.Key, e.Status, e.UpdatedAt))
            .ToListAsync(ct);
        Response = new(project.Name, access.Role, typeCount, published, drafts, recent);
    }
}
