using FastEndpoints;
using FastEndpoints.Security;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Tenancy.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HeadlessCms.Api.Endpoints.Tenants;

public sealed class TransferTenantOwnership(ApplicationDbContext db)
    : Endpoint<
        TransferTenantOwnershipRequest,
        IReadOnlyList<TransferTenantOwnershipMemberResponse>>
{
    public override void Configure()
    {
        Post("tenants/{tenantId:guid}/ownership-transfer");
        Claims("sub");
    }

    public override async Task HandleAsync(
        TransferTenantOwnershipRequest request,
        CancellationToken ct)
    {
        var ownerId = User.ClaimValue("sub")!;
        var owner = await db.TenantMemberships
            .Include(membership => membership.User)
            .SingleOrDefaultAsync(
                membership =>
                    membership.TenantId == request.TenantId &&
                    membership.UserId == ownerId &&
                    membership.Role == TenantRole.Owner,
                ct);
        var nextOwner = await db.TenantMemberships
            .Include(membership => membership.User)
            .SingleOrDefaultAsync(
                membership =>
                    membership.TenantId == request.TenantId &&
                    membership.UserId == request.NewOwnerUserId &&
                    membership.Role != TenantRole.Owner,
                ct);

        if (owner is null || nextOwner is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        IDbContextTransaction? transaction = null;
        if (db.Database.IsRelational())
            transaction = await db.Database.BeginTransactionAsync(ct);

        try
        {
            owner.Role = TenantRole.Editor;
            await db.SaveChangesAsync(ct);
            nextOwner.Role = TenantRole.Owner;
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
            new TransferTenantOwnershipMemberResponse(
                owner.UserId,
                owner.User.Email,
                owner.Role,
                owner.JoinedAt),
            new TransferTenantOwnershipMemberResponse(
                nextOwner.UserId,
                nextOwner.User.Email,
                nextOwner.Role,
                nextOwner.JoinedAt)
        ];
    }
}

public sealed class TransferTenantOwnershipRequest
{
    public Guid TenantId { get; init; }
    public required string NewOwnerUserId { get; init; }
}

public sealed record TransferTenantOwnershipMemberResponse(
    string UserId,
    string Email,
    TenantRole Role,
    DateTime JoinedAt);
