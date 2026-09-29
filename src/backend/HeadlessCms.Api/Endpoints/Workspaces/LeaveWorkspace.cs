using FastEndpoints;
using FastEndpoints.Security;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;
using Microsoft.EntityFrameworkCore;

using HeadlessCms.Api.Documentation;

namespace HeadlessCms.Api.Endpoints.Workspaces;

public sealed class LeaveWorkspace(
    ApplicationDbContext db)
    : Endpoint<LeaveWorkspaceRequest>
{
    public override void Configure()
    {
        Delete("me/workspaces/{workspaceId:guid}");
        Claims("sub");
        Description(b => b.WithTags("Workspaces"), clearDefaults: true);
        Summary((EndpointSummary s) =>
        {
            s.Summary = "Leave a workspace";
            s.Description = "Access: Workspace Editor or Member.\n\nRemoves the caller's membership. Owners receive 409 owner_must_transfer and must transfer ownership first.";
            s.Params["WorkspaceId"] = "Workspace UUID.";
            s.Response(204, "No content.");
            s.Response<FastEndpoints.ProblemDetails>(400, "Invalid request or validation failure.", "application/problem+json");
            s.Response<ApiProblem>(401, "Authentication is required, or the session has expired.", "application/problem+json");
            s.Response(404, "Resource not found or inaccessible.");
            s.Response<ApiProblem>(409, "Transfer ownership before leaving (owner_must_transfer).", "application/problem+json");
        });
    }

    public override async Task HandleAsync(LeaveWorkspaceRequest request, CancellationToken ct)
    {
        var userId = User.ClaimValue("sub")!;
        var membership = await db.WorkspaceMemberships.SingleOrDefaultAsync(
            candidate =>
                candidate.WorkspaceId == request.WorkspaceId &&
                candidate.UserId == userId,
            ct);

        if (membership is null || !WorkspaceAccessRoles.Members.Contains(membership.Role))
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (membership.Role == WorkspaceRole.Owner)
        {
            await ApiErrors.SendAsync(
                HttpContext,
                StatusCodes.Status409Conflict,
                "owner_must_transfer",
                "Transfer ownership before leaving the workspace.",
                ct);
            return;
        }

        db.WorkspaceMemberships.Remove(membership);
        await db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

public sealed class LeaveWorkspaceRequest
{
    public Guid WorkspaceId { get; init; }
}
