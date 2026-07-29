using System.Security.Cryptography;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Auth.Services;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Tenancy.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Tenancy.Services;

public sealed record CreatedTenantInvitation(
    Guid TenantId,
    Guid InvitationId,
    string Token,
    DateTime ExpiresAt);

public sealed record InvitationPreview(
    Guid InvitationId,
    Guid TenantId,
    string TenantName,
    string Email,
    TenantRole Role,
    DateTime ExpiresAt,
    InvitationStatus Status);

public sealed record InvitationRegistration(User User, TenantMembership Membership, Tenant Tenant);

public sealed class InvitationFlowException(
    int statusCode,
    string code,
    string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
}

public class TenantInvitationService(
    ApplicationDbContext db,
    IConfiguration configuration,
    IPasswordHasher<User> passwordHasher,
    IInvitationEmailSender emailSender)
{
    public async Task<CreatedTenantInvitation> CreateTenantWithOwnerInvitationAsync(
        string platformAdminUserId,
        string tenantName,
        string ownerEmail,
        CancellationToken ct = default)
    {
        var isPlatformAdmin = await db.Users.AnyAsync(
            user =>
                user.Id == platformAdminUserId &&
                user.PlatformRole == PlatformRole.PlatformAdmin,
            ct);

        if (!isPlatformAdmin)
            throw Forbidden("Only a platform administrator can create tenants.");

        var normalizedName = NormalizeTenantName(tenantName);
        var tenant = new Tenant { Name = normalizedName };
        var (invitation, token) = CreateInvitationEntity(
            ownerEmail,
            TenantRole.Owner,
            platformAdminUserId);

        tenant.Invitations.Add(invitation);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(ct);
        await SendInvitationAsync(invitation, tenant.Name, token, ct);

        return new CreatedTenantInvitation(
            tenant.Id,
            invitation.Id,
            token,
            invitation.ExpiresAt);
    }

    public async Task<CreatedTenantInvitation> CreateInvitationAsync(
        string ownerUserId,
        Guid tenantId,
        string email,
        TenantRole role,
        CancellationToken ct = default)
    {
        if (role == TenantRole.Owner)
            throw BadRequest("Tenant owners can invite editors or members.");

        var tenant = await db.Tenants.SingleOrDefaultAsync(candidate => candidate.Id == tenantId, ct)
                     ?? throw NotFound("Tenant not found.");

        var ownerMembership = await db.TenantMemberships.AnyAsync(
            membership =>
                membership.UserId == ownerUserId &&
                membership.TenantId == tenantId &&
                membership.Role == TenantRole.Owner,
            ct);

        if (!ownerMembership)
            throw Forbidden("Only a tenant owner can invite users.");

        var normalizedEmail = EmailNormalizer.Normalize(email);
        var alreadyMember = await db.TenantMemberships.AnyAsync(
            membership =>
                membership.TenantId == tenantId &&
                membership.User.Email == normalizedEmail,
            ct);

        if (alreadyMember)
            throw Conflict("The user is already a member of this tenant.");

        var pendingInvitation = await db.TenantInvitations.AnyAsync(
            invitation =>
                invitation.TenantId == tenantId &&
                invitation.Email == normalizedEmail &&
                invitation.AcceptedAt == null &&
                invitation.RevokedAt == null,
            ct);

        if (pendingInvitation)
            throw Conflict("A pending invitation already exists for this email.");

        var (invitation, token) = CreateInvitationEntity(
            normalizedEmail,
            role,
            ownerUserId);
        invitation.TenantId = tenantId;

        db.TenantInvitations.Add(invitation);
        await db.SaveChangesAsync(ct);
        await SendInvitationAsync(invitation, tenant.Name, token, ct);

        return new CreatedTenantInvitation(
            tenantId,
            invitation.Id,
            token,
            invitation.ExpiresAt);
    }

    public async Task<InvitationPreview> PreviewAsync(
        string token,
        CancellationToken ct = default)
    {
        var invitation = await FindByTokenAsync(token, ct);
        var status = GetStatus(invitation);

        if (status == InvitationStatus.Expired || status == InvitationStatus.Revoked)
            throw Gone("The invitation has expired or was revoked.");

        if (status == InvitationStatus.Accepted)
            throw Conflict("The invitation has already been accepted.");

        return new InvitationPreview(
            invitation.Id,
            invitation.TenantId,
            invitation.Tenant.Name,
            invitation.Email,
            invitation.Role,
            invitation.ExpiresAt,
            status);
    }

    public async Task<InvitationRegistration> RegisterAsync(
        string token,
        string password,
        CancellationToken ct = default)
    {
        var invitation = await FindActiveByTokenAsync(token, ct);

        if (await db.Users.AnyAsync(user => user.Email == invitation.Email, ct))
            throw Conflict("An account already exists for this email. Log in to accept the invitation.");

        var user = new User
        {
            Email = invitation.Email,
            PlatformRole = PlatformRole.User
        };
        user.PasswordHash = passwordHasher.HashPassword(user, password);

        var membership = new TenantMembership
        {
            TenantId = invitation.TenantId,
            User = user,
            Role = invitation.Role
        };

        invitation.AcceptedAt = DateTime.UtcNow;
        invitation.AcceptedByUser = user;
        db.Users.Add(user);
        db.TenantMemberships.Add(membership);
        await db.SaveChangesAsync(ct);

        return new InvitationRegistration(user, membership, invitation.Tenant);
    }

    public async Task<TenantMembership> AcceptInvitationAsync(
        string userId,
        string token,
        CancellationToken ct = default)
    {
        var invitation = await FindActiveByTokenAsync(token, ct);
        var user = await db.Users.SingleOrDefaultAsync(candidate => candidate.Id == userId, ct)
                   ?? throw NotFound("User not found.");

        if (!string.Equals(user.Email, invitation.Email, StringComparison.Ordinal))
            throw Forbidden("The invitation belongs to a different email address.");

        if (await db.TenantMemberships.AnyAsync(
                membership =>
                    membership.TenantId == invitation.TenantId &&
                    membership.UserId == userId,
                ct))
        {
            throw Conflict("The user is already a member of this tenant.");
        }

        var membership = new TenantMembership
        {
            TenantId = invitation.TenantId,
            Tenant = invitation.Tenant,
            UserId = userId,
            Role = invitation.Role
        };

        invitation.AcceptedAt = DateTime.UtcNow;
        invitation.AcceptedByUserId = userId;
        db.TenantMemberships.Add(membership);
        await db.SaveChangesAsync(ct);

        return membership;
    }

    public async Task<CreatedTenantInvitation> ResendAsync(
        TenantInvitation invitation,
        CancellationToken ct = default)
    {
        var status = GetStatus(invitation);
        if (status == InvitationStatus.Accepted)
            throw Conflict("Accepted invitations cannot be resent.");
        if (status == InvitationStatus.Revoked)
            throw Gone("Revoked invitations cannot be resent.");

        var token = CreateToken();
        invitation.TokenHash = TokenHasher.Hash(token);
        invitation.ExpiresAt = DateTime.UtcNow.AddHours(GetValidityHours());
        await db.SaveChangesAsync(ct);

        var tenantName = await db.Tenants
            .Where(tenant => tenant.Id == invitation.TenantId)
            .Select(tenant => tenant.Name)
            .SingleAsync(ct);
        await SendInvitationAsync(invitation, tenantName, token, ct);

        return new CreatedTenantInvitation(
            invitation.TenantId,
            invitation.Id,
            token,
            invitation.ExpiresAt);
    }

    public async Task RevokeAsync(TenantInvitation invitation, CancellationToken ct = default)
    {
        if (invitation.AcceptedAt is not null)
            throw Conflict("Accepted invitations cannot be revoked.");

        invitation.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public static InvitationStatus GetStatus(TenantInvitation invitation)
    {
        if (invitation.AcceptedAt is not null)
            return InvitationStatus.Accepted;
        if (invitation.RevokedAt is not null)
            return InvitationStatus.Revoked;
        return DateTime.UtcNow >= invitation.ExpiresAt
            ? InvitationStatus.Expired
            : InvitationStatus.Pending;
    }

    private async Task<TenantInvitation> FindByTokenAsync(
        string token,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw NotFound("Invitation not found.");

        var hash = TokenHasher.Hash(token);
        return await db.TenantInvitations
                   .Include(invitation => invitation.Tenant)
                   .SingleOrDefaultAsync(invitation => invitation.TokenHash == hash, ct)
               ?? throw NotFound("Invitation not found.");
    }

    private async Task<TenantInvitation> FindActiveByTokenAsync(
        string token,
        CancellationToken ct)
    {
        var invitation = await FindByTokenAsync(token, ct);
        var status = GetStatus(invitation);

        return status switch
        {
            InvitationStatus.Pending => invitation,
            InvitationStatus.Accepted => throw Conflict("The invitation has already been accepted."),
            _ => throw Gone("The invitation has expired or was revoked.")
        };
    }

    private (TenantInvitation Invitation, string Token) CreateInvitationEntity(
        string email,
        TenantRole role,
        string invitedByUserId)
    {
        var token = CreateToken();
        var invitation = new TenantInvitation
        {
            Email = EmailNormalizer.Normalize(email),
            Role = role,
            TokenHash = TokenHasher.Hash(token),
            ExpiresAt = DateTime.UtcNow.AddHours(GetValidityHours()),
            InvitedByUserId = invitedByUserId
        };

        return (invitation, token);
    }

    private async Task SendInvitationAsync(
        TenantInvitation invitation,
        string tenantName,
        string token,
        CancellationToken ct)
    {
        await emailSender.SendAsync(
            invitation.Email,
            tenantName,
            invitation.Role,
            token,
            ct);
        invitation.LastSentAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private int GetValidityHours()
    {
        var hours = configuration.GetValue<int?>("Tenancy:InvitationExpirationHours")
                    ?? throw new InvalidOperationException(
                        "Required configuration 'Tenancy:InvitationExpirationHours' is missing.");

        return hours > 0
            ? hours
            : throw new InvalidOperationException(
                "Tenancy invitation expiration must be greater than zero.");
    }

    private static string CreateToken() =>
        WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    private static string NormalizeTenantName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
            throw BadRequest("A tenant name between 1 and 100 characters is required.");

        return name.Trim();
    }

    private static InvitationFlowException BadRequest(string message) =>
        new(StatusCodes.Status400BadRequest, "invalid_request", message);

    private static InvitationFlowException Forbidden(string message) =>
        new(StatusCodes.Status403Forbidden, "forbidden", message);

    private static InvitationFlowException NotFound(string message) =>
        new(StatusCodes.Status404NotFound, "not_found", message);

    private static InvitationFlowException Conflict(string message) =>
        new(StatusCodes.Status409Conflict, "conflict", message);

    private static InvitationFlowException Gone(string message) =>
        new(StatusCodes.Status410Gone, "invitation_gone", message);
}
