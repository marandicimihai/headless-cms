using FastEndpoints;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;
using Microsoft.EntityFrameworkCore;

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
            all.Skip((page - 1) * size).Take(size).Select(ToResponse).ToList(),
            page,
            size,
            all.Count);
    }

    private static (int Page, int Size) NormalizePage(int page, int size) =>
        (Math.Max(page, 1), Math.Clamp(size, 1, 100));

    private static ListWorkspaceInvitationsItemResponse ToResponse(
        WorkspaceInvitation invitation) =>
        new(
            invitation.Id,
            invitation.WorkspaceId,
            invitation.Email,
            invitation.Role,
            WorkspaceInvitationService.GetStatus(invitation),
            invitation.CreatedAt,
            invitation.ExpiresAt,
            invitation.LastSentAt,
            invitation.AcceptedAt,
            invitation.RevokedAt);
}

public sealed class ListWorkspaceInvitationsRequest
{
    public Guid WorkspaceId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public InvitationStatus? Status { get; init; }
}

public sealed record ListWorkspaceInvitationsItemResponse(
    Guid Id,
    Guid WorkspaceId,
    string Email,
    WorkspaceRole Role,
    InvitationStatus Status,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    DateTime? LastSentAt,
    DateTime? AcceptedAt,
    DateTime? RevokedAt);

public sealed record ListWorkspaceInvitationsResponse(
    IReadOnlyList<ListWorkspaceInvitationsItemResponse> Items,
    int Page,
    int PageSize,
    int Total);
