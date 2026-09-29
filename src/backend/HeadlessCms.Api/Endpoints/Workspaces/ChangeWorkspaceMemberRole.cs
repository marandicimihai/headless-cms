using FastEndpoints;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;
using Microsoft.EntityFrameworkCore;

using HeadlessCms.Api.Documentation;

namespace HeadlessCms.Api.Endpoints.Workspaces;

public sealed class ChangeWorkspaceMemberRole(
    ApplicationDbContext db,
    WorkspaceAccessService workspaceAccess)
    : Endpoint<ChangeWorkspaceMemberRoleRequest, ChangeWorkspaceMemberRoleResponse>
{
    public override void Configure()
    {
        Patch("workspaces/{workspaceId:guid}/members/{userId}");
        Claims("sub");
        Description(b => b.WithTags("Members"), clearDefaults: true);
        Summary((EndpointSummary s) =>
        {
            s.Summary = "Change a member's role";
            s.Description = "Access: Workspace Owner.\n\nChanges a non-owner member to editor or member. Use ownership-transfer to change the owner.";
            s.Params["WorkspaceId"] = "Workspace UUID.";
            s.Params["UserId"] = "User identifier of the target workspace member.";
            s.Params["Role"] = "The role to assign: editor or member.";
            s.ExampleRequest = new { Role = "editor" };
            s.Response<ChangeWorkspaceMemberRoleResponse>(200, "Success.");
            s.ResponseExamples[200] = ApiExamples.Member;
            s.Response<ApiProblem>(400, "Invalid request or validation failure.", "application/problem+json");
            s.Response<ApiProblem>(401, "Authentication is required, or the session has expired.", "application/problem+json");
            s.Response(404, "Resource not found or inaccessible.");
        });
    }

    public override async Task HandleAsync(
        ChangeWorkspaceMemberRoleRequest request,
        CancellationToken ct)
    {
        if (request.Role is not (WorkspaceRole.Editor or WorkspaceRole.Member))
        {
            await ApiErrors.SendAsync(
                HttpContext,
                StatusCodes.Status400BadRequest,
                "invalid_role",
                "Role must be Editor or Member.",
                ct);
            return;
        }

        if (await workspaceAccess.ResolveAsync(
                User,
                request.WorkspaceId,
                WorkspaceAccessRoles.Owners,
                ct) is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var membership = await db.WorkspaceMemberships
            .Include(item => item.User)
            .SingleOrDefaultAsync(
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

        membership.Role = request.Role;
        await db.SaveChangesAsync(ct);
        Response = new ChangeWorkspaceMemberRoleResponse(
            membership.UserId,
            membership.User.Email,
            membership.Role,
            membership.JoinedAt);
    }
}

public sealed class ChangeWorkspaceMemberRoleRequest
{
    public Guid WorkspaceId { get; init; }
    public string UserId { get; init; } = default!;
    public WorkspaceRole Role { get; init; }
}

public sealed record ChangeWorkspaceMemberRoleResponse(
    string UserId,
    string Email,
    WorkspaceRole Role,
    DateTime JoinedAt);
