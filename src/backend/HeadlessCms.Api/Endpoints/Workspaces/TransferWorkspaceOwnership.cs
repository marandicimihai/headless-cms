using FastEndpoints;
using FastEndpoints.Security;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

using HeadlessCms.Api.Documentation;

namespace HeadlessCms.Api.Endpoints.Workspaces;

public sealed class TransferWorkspaceOwnership(
    ApplicationDbContext db,
    WorkspaceOwnershipLimitService ownershipLimits)
    : Endpoint<
        TransferWorkspaceOwnershipRequest,
        IReadOnlyList<TransferWorkspaceOwnershipMemberResponse>>
{
    public override void Configure()
    {
        Post("workspaces/{workspaceId:guid}/ownership-transfer");
        Claims("sub");
        Description(b => b.WithTags("Members"), clearDefaults: true);
        Summary((EndpointSummary s) =>
        {
            s.Summary = "Transfer ownership";
            s.Description = "Access: Workspace Owner.\n\nTransfers ownership to an existing non-owner member; the previous owner becomes Editor. Returns both updated memberships. The recipient's ownership limit is enforced.";
            s.Params["WorkspaceId"] = "Workspace UUID.";
            s.Params["NewOwnerUserId"] = "User identifier of an existing non-owner member.";
            s.ExampleRequest = new { NewOwnerUserId = "next-owner" };
            s.Response<IReadOnlyList<TransferWorkspaceOwnershipMemberResponse>>(200, "Success.");
            s.ResponseExamples[200] = new object[] { ApiExamples.Member, new { UserId = "next-owner", Email = "owner@example.com", Role = "owner", JoinedAt = ApiExamples.Timestamp } };
            s.Response<FastEndpoints.ProblemDetails>(400, "Invalid request or validation failure.", "application/problem+json");
            s.Response<ApiProblem>(401, "Authentication is required, or the session has expired.", "application/problem+json");
            s.Response(404, "Resource not found or inaccessible.");
            s.Response<ApiProblem>(409, "The ownership limit has been reached (workspace_limit_reached).", "application/problem+json");
        });
    }

    public override async Task HandleAsync(
        TransferWorkspaceOwnershipRequest request,
        CancellationToken ct)
    {
        var userId = User.ClaimValue("sub")!;
        var owner = await db.WorkspaceMemberships
            .Include(membership => membership.User)
            .SingleOrDefaultAsync(
                membership =>
                    membership.WorkspaceId == request.WorkspaceId &&
                    membership.UserId == userId &&
                    membership.Role == WorkspaceRole.Owner,
                ct);
        if (owner is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var nextOwner = await db.WorkspaceMemberships
            .Include(membership => membership.User)
            .SingleOrDefaultAsync(
                membership =>
                    membership.WorkspaceId == request.WorkspaceId &&
                    membership.UserId == request.NewOwnerUserId &&
                    membership.Role != WorkspaceRole.Owner,
                ct);

        if (nextOwner is null)
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
