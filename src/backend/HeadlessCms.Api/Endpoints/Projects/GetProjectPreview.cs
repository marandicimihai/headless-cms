using FastEndpoints;
using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;
using Microsoft.EntityFrameworkCore;

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
        var published = await entries.CountAsync(e => e.Status == ContentEntryStatus.Published, ct);
        var drafts = await entries.CountAsync(e => e.Status == ContentEntryStatus.Draft, ct);
        var recent = await entries.OrderByDescending(e => e.UpdatedAt).ThenBy(e => e.Id)
            .Take(10).Select(e => new ProjectPreviewEntry(e.Id, e.ContentType.Key, e.Status, e.UpdatedAt))
            .ToListAsync(ct);
        Response = new(project.Name, access.Role, typeCount, published, drafts, recent);
    }
}
