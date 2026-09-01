using FastEndpoints;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HeadlessCms.Api.Endpoints.Workspaces;

public sealed class TransferWorkspaceOwnership(
    ApplicationDbContext db,
    WorkspaceOwnershipLimitService ownershipLimits,
    WorkspaceAccessService workspaceAccess)
    : Endpoint<
        TransferWorkspaceOwnershipRequest,
        IReadOnlyList<TransferWorkspaceOwnershipMemberResponse>>
{
    public override void Configure()
    {
        Post("workspaces/{workspaceId:guid}/ownership-transfer");
        Claims("sub");
    }

    public override async Task HandleAsync(
        TransferWorkspaceOwnershipRequest request,
        CancellationToken ct)
    {
        var access = await workspaceAccess.ResolveAsync(
            User,
            request.WorkspaceId,
            WorkspaceAccessRoles.Owners,
            ct);
        if (access is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var owner = await db.WorkspaceMemberships
            .Include(membership => membership.User)
            .SingleOrDefaultAsync(
                membership =>
                    membership.WorkspaceId == request.WorkspaceId &&
                    membership.UserId == access.UserId &&
                    membership.Role == WorkspaceRole.Owner,
                ct);
        var nextOwner = await db.WorkspaceMemberships
            .Include(membership => membership.User)
            .SingleOrDefaultAsync(
                membership =>
                    membership.WorkspaceId == request.WorkspaceId &&
                    membership.UserId == request.NewOwnerUserId &&
                    membership.Role != WorkspaceRole.Owner,
                ct);

        if (owner is null || nextOwner is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (!await ownershipLimits.CanOwnAnotherWorkspaceAsync(nextOwner.UserId, ct))
        {
            await ApiErrors.SendAsync(
                HttpContext,
                StatusCodes.Status409Conflict,
                "workspace_limit_reached",
                $"A user can own at most {ownershipLimits.MaximumOwnedWorkspaces} workspaces.",
                ct);
            return;
        }

        IDbContextTransaction? transaction = null;
        if (db.Database.IsRelational())
            transaction = await db.Database.BeginTransactionAsync(ct);

        try
        {
            owner.Role = WorkspaceRole.Editor;
            await db.SaveChangesAsync(ct);
            nextOwner.Role = WorkspaceRole.Owner;
            await db.SaveChangesAsync(ct);
            if (transaction is not null)
                await transaction.CommitAsync(ct);
        }
        catch
        {
            if (transaction is not null)
                await transaction.RollbackAsync(ct);
            throw;
        }
        finally
        {
            if (transaction is not null)
                await transaction.DisposeAsync();
        }

        Response =
        [
            new TransferWorkspaceOwnershipMemberResponse(
                owner.UserId,
                owner.User.Email,
                owner.Role,
                owner.JoinedAt),
            new TransferWorkspaceOwnershipMemberResponse(
                nextOwner.UserId,
                nextOwner.User.Email,
                nextOwner.Role,
                nextOwner.JoinedAt)
        ];
    }
}

public sealed class TransferWorkspaceOwnershipRequest
{
    public Guid WorkspaceId { get; init; }
    public required string NewOwnerUserId { get; init; }
}

public sealed record TransferWorkspaceOwnershipMemberResponse(
    string UserId,
    string Email,
    WorkspaceRole Role,
    DateTime JoinedAt);
