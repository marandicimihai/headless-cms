using FastEndpoints;
using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Endpoints.Workspaces;

public sealed class GetWorkspacePreviewRequest
{
    public Guid WorkspaceId { get; init; }
}

public sealed record WorkspacePreviewProject(
    Guid Id, string Name, int ContentTypeCount, int EntryCount, DateTime? LastContentUpdatedAt);

public sealed record GetWorkspacePreviewResponse(
    string Name, WorkspaceRole CurrentRole, int ProjectCount, int EntryCount,
    int PublishedEntryCount, int DraftEntryCount, int MemberCount,
    int? PendingInvitationCount, List<WorkspacePreviewProject> Projects);

public sealed class GetWorkspacePreview(ApplicationDbContext db, WorkspaceAccessService workspaceAccess)
    : Endpoint<GetWorkspacePreviewRequest, GetWorkspacePreviewResponse>
{
    public override void Configure()
    {
        Get("workspaces/{workspaceId:guid}/preview");
        Claims("sub");
    }

    public override async Task HandleAsync(GetWorkspacePreviewRequest request, CancellationToken ct)
    {
        var access = await workspaceAccess.ResolveAsync(User, request.WorkspaceId, WorkspaceAccessRoles.Members, ct);
        if (access is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var workspace = await db.Workspaces.AsNoTracking().SingleAsync(w => w.Id == request.WorkspaceId, ct);
        var projects = await db.Projects.AsNoTracking()
            .Where(p => p.WorkspaceId == request.WorkspaceId)
            .OrderBy(p => p.Name).ThenBy(p => p.Id)
            .Select(p => new WorkspacePreviewProject(
                p.Id, p.Name, p.ContentTypes.Count,
                db.ContentEntries.Count(e => e.WorkspaceId == request.WorkspaceId && e.ProjectId == p.Id),
                db.ContentEntries.Where(e => e.WorkspaceId == request.WorkspaceId && e.ProjectId == p.Id)
                    .Max(e => (DateTime?)e.UpdatedAt)))
            .ToListAsync(ct);
        var entries = db.ContentEntries.Where(e => e.WorkspaceId == request.WorkspaceId);
        var published = await entries.CountAsync(e => e.Status == ContentEntryStatus.Published, ct);
        var drafts = await entries.CountAsync(e => e.Status == ContentEntryStatus.Draft, ct);
        var members = await db.WorkspaceMemberships.CountAsync(m => m.WorkspaceId == request.WorkspaceId, ct);
        int? pending = null;
        if (access.Role == WorkspaceRole.Owner)
        {
            var now = DateTime.UtcNow;
            pending = await db.WorkspaceInvitations.CountAsync(i =>
                i.WorkspaceId == request.WorkspaceId && i.AcceptedAt == null &&
                i.RevokedAt == null && i.ExpiresAt > now, ct);
        }

        Response = new(workspace.Name, access.Role, projects.Count, published + drafts,
            published, drafts, members, pending, projects);
    }
}
