using FastEndpoints;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Workspaces.Services;
using Microsoft.EntityFrameworkCore;

using HeadlessCms.Api.Documentation;

namespace HeadlessCms.Api.Endpoints.Workspaces;

public sealed class DeleteWorkspace(
    ApplicationDbContext db,
    WorkspaceAccessService workspaceAccess) : Endpoint<DeleteWorkspaceRequest>
{
    public override void Configure()
    {
        Delete("workspaces/{workspaceId:guid}");
        Claims("sub");
        Description(b => b.WithTags("Workspaces"), clearDefaults: true);
        Summary((EndpointSummary s) =>
        {
            s.Summary = "Delete a workspace";
            s.Description = "Access: Workspace Owner.\n\nPermanently deletes the workspace and its projects, content types, entries, memberships, and invitations. No request body or name confirmation is required by the API.";
            s.Params["WorkspaceId"] = "Workspace UUID.";
            s.Response(204, "No content.");
            s.Response<FastEndpoints.ProblemDetails>(400, "Invalid request or validation failure.", "application/problem+json");
            s.Response<ApiProblem>(401, "Authentication is required, or the session has expired.", "application/problem+json");
            s.Response(404, "Resource not found or inaccessible.");
        });
    }

    public override async Task HandleAsync(DeleteWorkspaceRequest request, CancellationToken ct)
    {
        var access = await workspaceAccess.ResolveAsync(
            User, request.WorkspaceId, WorkspaceAccessRoles.Owners, ct);
        if (access is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var workspace = await db.Workspaces.SingleOrDefaultAsync(
            candidate => candidate.Id == request.WorkspaceId, ct);
        if (workspace is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(ct)
            : null;
        var entries = db.ContentEntries.Where(entry => entry.WorkspaceId == request.WorkspaceId);
        if (db.Database.IsRelational())
            await entries.ExecuteDeleteAsync(ct);
        else
        {
            db.ContentEntries.RemoveRange(await entries.ToListAsync(ct));
            db.ContentFields.RemoveRange(await db.ContentFields
                .Where(field => field.WorkspaceId == request.WorkspaceId)
                .ToListAsync(ct));
            db.ContentTypes.RemoveRange(await db.ContentTypes
                .Where(contentType => contentType.WorkspaceId == request.WorkspaceId)
                .ToListAsync(ct));
            db.Projects.RemoveRange(await db.Projects
                .Where(project => project.WorkspaceId == request.WorkspaceId)
                .ToListAsync(ct));
            db.WorkspaceInvitations.RemoveRange(await db.WorkspaceInvitations
                .Where(invitation => invitation.WorkspaceId == request.WorkspaceId)
                .ToListAsync(ct));
            db.WorkspaceMemberships.RemoveRange(await db.WorkspaceMemberships
                .Where(membership => membership.WorkspaceId == request.WorkspaceId)
                .ToListAsync(ct));
        }
        // Database cascades remove projects, content types, fields, memberships, and invitations.
        db.Workspaces.Remove(workspace);
        await db.SaveChangesAsync(ct);
        if (transaction is not null)
            await transaction.CommitAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

public sealed class DeleteWorkspaceRequest
{
    public Guid WorkspaceId { get; init; }
}
