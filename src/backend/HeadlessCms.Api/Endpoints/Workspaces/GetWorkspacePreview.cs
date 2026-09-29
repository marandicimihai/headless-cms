using FastEndpoints;
using HeadlessCms.Api.Content.Models;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;
using Microsoft.EntityFrameworkCore;

using HeadlessCms.Api.Documentation;

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
        Description(b => b.WithTags("Workspaces"), clearDefaults: true);
        Summary((EndpointSummary s) =>
        {
            s.Summary = "Get workspace overview";
            s.Description = "Access: Workspace Owner, Editor, or Member.\n\nReturns project and entry counts plus project summaries. pendingInvitationCount is populated only for owners and null for other members.";
            s.Params["WorkspaceId"] = "Workspace UUID.";
            s.Response<GetWorkspacePreviewResponse>(200, "Success.");
            s.ResponseExamples[200] = new { Name = "Editorial", CurrentRole = "owner", ProjectCount = 1, EntryCount = 1, PublishedEntryCount = 0, DraftEntryCount = 1, MemberCount = 2, PendingInvitationCount = 0, Projects = new[] { new { Id = ApiExamples.ProjectId, Name = "Website", ContentTypeCount = 1, EntryCount = 1, LastContentUpdatedAt = ApiExamples.Timestamp } } };
            s.Response<FastEndpoints.ProblemDetails>(400, "Invalid request or validation failure.", "application/problem+json");
            s.Response<ApiProblem>(401, "Authentication is required, or the session has expired.", "application/problem+json");
            s.Response(404, "Resource not found or inaccessible.");
        });
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
        var counts = await entries.GroupBy(e => 1)
            .Select(group => new
            {
                Published = group.Count(e => e.Status == ContentEntryStatus.Published),
                Drafts = group.Count(e => e.Status == ContentEntryStatus.Draft)
            })
            .SingleOrDefaultAsync(ct);
        var published = counts?.Published ?? 0;
        var drafts = counts?.Drafts ?? 0;
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
