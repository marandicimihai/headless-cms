using FastEndpoints;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;
using Microsoft.EntityFrameworkCore;

using HeadlessCms.Api.Documentation;

namespace HeadlessCms.Api.Endpoints.Workspaces;

public sealed class RemoveWorkspaceMember(
    ApplicationDbContext db,
    WorkspaceAccessService workspaceAccess)
    : Endpoint<RemoveWorkspaceMemberRequest>
{
    public override void Configure()
    {
        Delete("workspaces/{workspaceId:guid}/members/{userId}");
        Claims("sub");
        Description(b => b.WithTags("Members"), clearDefaults: true);
        Summary((EndpointSummary s) =>
        {
            s.Summary = "Remove a member";
            s.Description = "Access: Workspace Owner.\n\nRemoves a non-owner membership. Owners cannot be removed using this endpoint.";
            s.Params["WorkspaceId"] = "Workspace UUID.";
            s.Params["UserId"] = "User identifier of the target workspace member.";
            s.Response(204, "No content.");
            s.Response<FastEndpoints.ProblemDetails>(400, "Invalid request or validation failure.", "application/problem+json");
            s.Response<ApiProblem>(401, "Authentication is required, or the session has expired.", "application/problem+json");
            s.Response(404, "Resource not found or inaccessible.");
        });
    }

    public override async Task HandleAsync(
        RemoveWorkspaceMemberRequest request,
        CancellationToken ct)
    {
        if (await workspaceAccess.ResolveAsync(
                User,
                request.WorkspaceId,
                WorkspaceAccessRoles.Owners,
                ct) is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var membership = await db.WorkspaceMemberships.SingleOrDefaultAsync(
            item =>
                item.WorkspaceId == request.WorkspaceId &&
                item.UserId == request.UserId &&
                item.Role != WorkspaceRole.Owner,
            ct);
        if (membership is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        db.WorkspaceMemberships.Remove(membership);
        await db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

public sealed class RemoveWorkspaceMemberRequest
{
    public Guid WorkspaceId { get; init; }
    public string UserId { get; init; } = default!;
}
