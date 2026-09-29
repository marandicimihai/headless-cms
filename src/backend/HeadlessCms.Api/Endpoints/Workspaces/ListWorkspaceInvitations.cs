using FastEndpoints;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;
using Microsoft.EntityFrameworkCore;

using HeadlessCms.Api.Documentation;

namespace HeadlessCms.Api.Endpoints.Workspaces;

public sealed class ListWorkspaceInvitations(
    ApplicationDbContext db,
    WorkspaceAccessService workspaceAccess)
    : Endpoint<ListWorkspaceInvitationsRequest, ListWorkspaceInvitationsResponse>
{
    public override void Configure()
    {
        Get("workspaces/{workspaceId:guid}/invitations");
        Claims("sub");
        Description(b => b.WithTags("Invitations"), clearDefaults: true);
        Summary((EndpointSummary s) =>
        {
            s.Summary = "List invitations";
            s.Description = "Access: Workspace Owner; PlatformAdmin may list owner invitations only in an ownerless workspace.\n\nLists invitations with optional status filtering. Never returns invitationUrl or a token. Page is clamped to at least 1 and pageSize to 1–100; defaults are 1 and 20.";
            s.Params["WorkspaceId"] = "Workspace UUID.";
            s.Params["Page"] = "Page number, starting at 1 (default 1).";
            s.Params["PageSize"] = "Items per page (maximum 100). Default 20; values are clamped to 1–100.";
            s.Params["Status"] = "Optional status filter.";
            s.Response<ListWorkspaceInvitationsResponse>(200, "Success.");
            s.ResponseExamples[200] = ApiExamples.Page(ApiExamples.Invitation);
            s.Response<FastEndpoints.ProblemDetails>(400, "Invalid request or validation failure.", "application/problem+json");
            s.Response<ApiProblem>(401, "Authentication is required, or the session has expired.", "application/problem+json");
            s.Response(404, "Resource not found or inaccessible.");
        });
    }

    public override async Task HandleAsync(
        ListWorkspaceInvitationsRequest request,
        CancellationToken ct)
    {
        var isAdmin = User.IsInRole(nameof(PlatformRole.PlatformAdmin));
        var isOwner = await workspaceAccess.ResolveAsync(
            User,
            request.WorkspaceId,
            WorkspaceAccessRoles.Owners,
            ct) is not null;

        if (!isAdmin && !isOwner)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (isAdmin && !isOwner)
        {
            var workspaceExists = await db.Workspaces.AnyAsync(
                workspace => workspace.Id == request.WorkspaceId,
                ct);
            if (!workspaceExists)
            {
                await Send.NotFoundAsync(ct);
                return;
            }

            var workspaceHasOwner = await db.WorkspaceMemberships.AnyAsync(
                membership =>
                    membership.WorkspaceId == request.WorkspaceId &&
                    membership.Role == WorkspaceRole.Owner,
                ct);
            if (workspaceHasOwner)
            {
                await Send.NotFoundAsync(ct);
                return;
            }
        }

        var (page, size) = NormalizePage(request.Page, request.PageSize);
        var query = db.WorkspaceInvitations
            .AsNoTracking()
            .Where(invitation => invitation.WorkspaceId == request.WorkspaceId);

        if (isAdmin && !isOwner)
            query = query.Where(invitation => invitation.Role == WorkspaceRole.Owner);

        var all = await query.OrderByDescending(invitation => invitation.CreatedAt).ToListAsync(ct);
        if (request.Status is not null)
        {
            all = all.Where(invitation =>
                    WorkspaceInvitationService.GetStatus(invitation) == request.Status)
                .ToList();
        }

        Response = new ListWorkspaceInvitationsResponse(
            all.Skip((page - 1) * size).Take(size).Select(WorkspaceInvitationResponse.FromInvitation).ToList(),
            page,
            size,
            all.Count);
    }

    private static (int Page, int Size) NormalizePage(int page, int size) =>
        (Math.Max(page, 1), Math.Clamp(size, 1, 100));

}

public sealed class ListWorkspaceInvitationsRequest
{
    public Guid WorkspaceId { get; init; }
    [System.ComponentModel.DefaultValue(1)]
    [FastEndpoints.QueryParam]
    public int Page { get; init; } = 1;
    [System.ComponentModel.DefaultValue(20)]
    [FastEndpoints.QueryParam]
    public int PageSize { get; init; } = 20;
    [FastEndpoints.QueryParam]
    public InvitationStatus? Status { get; init; }
}

public sealed record ListWorkspaceInvitationsResponse(
    IReadOnlyList<WorkspaceInvitationResponse> Items,
    int Page,
    int PageSize,
    int Total);
