using System.Net.Mail;
using System.Security.Cryptography;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Auth.Services;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Tenancy.Models;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Tenancy.Services;

public sealed record CreatedTenantInvitation(
    Guid TenantId,
    Guid InvitationId,
    string Token,
    DateTime ExpiresAt);

public class TenantInvitationService(
    ApplicationDbContext db,
    IConfiguration configuration)
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
            throw new UnauthorizedAccessException("Only a platform administrator can create tenants.");

        if (string.IsNullOrWhiteSpace(tenantName) || tenantName.Trim().Length > 100)
        {
            throw new ArgumentException(
                "A tenant name between 1 and 100 characters is required.",
                nameof(tenantName));
        }

        var tenant = new Tenant { Name = tenantName.Trim() };
        var (invitation, token) = CreateInvitation(
            ownerEmail,
            TenantRole.Owner,
            platformAdminUserId);

        tenant.Invitations.Add(invitation);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(ct);

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
            throw new ArgumentException(
                "Tenant owners can invite editors or members.",
                nameof(role));

        var ownerMembership = await db.TenantMemberships.AnyAsync(
            membership =>
                membership.UserId == ownerUserId &&
                membership.TenantId == tenantId &&
                membership.Role == TenantRole.Owner,
            ct);

        if (!ownerMembership)
            throw new UnauthorizedAccessException("Only a tenant owner can invite users.");

        var (invitation, token) = CreateInvitation(email, role, ownerUserId);
        invitation.TenantId = tenantId;

        db.TenantInvitations.Add(invitation);
        await db.SaveChangesAsync(ct);

        return new CreatedTenantInvitation(
            tenantId,
            invitation.Id,
            token,
            invitation.ExpiresAt);
    }

    public async Task<TenantMembership> AcceptInvitationAsync(
        string userId,
        string token,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("An invitation token is required.", nameof(token));

        var tokenHash = TokenHasher.Hash(token);
        var invitation = await db.TenantInvitations.SingleOrDefaultAsync(
            candidate => candidate.TokenHash == tokenHash,
            ct);

        if (invitation is null ||
            invitation.AcceptedAt is not null ||
            DateTime.UtcNow >= invitation.ExpiresAt)
        {
            throw new InvalidOperationException("The invitation is invalid, expired, or already used.");
        }

        var user = await db.Users.SingleOrDefaultAsync(candidate => candidate.Id == userId, ct);

        if (user?.Email is null ||
            !string.Equals(NormalizeEmail(user.Email), invitation.Email, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException(
                "The invitation belongs to a different email address.");
        }

        var membershipExists = await db.TenantMemberships.AnyAsync(
            membership =>
                membership.TenantId == invitation.TenantId &&
                membership.UserId == userId,
            ct);

        if (membershipExists)
            throw new InvalidOperationException("The user is already a member of this tenant.");

        var membership = new TenantMembership
        {
            TenantId = invitation.TenantId,
            UserId = userId,
            Role = invitation.Role
        };

        invitation.AcceptedAt = DateTime.UtcNow;
        invitation.AcceptedByUserId = userId;
        db.TenantMemberships.Add(membership);
        await db.SaveChangesAsync(ct);

        return membership;
    }

    private (TenantInvitation Invitation, string Token) CreateInvitation(
        string email,
        TenantRole role,
        string invitedByUserId)
    {
        var validityHours =
            configuration.GetValue<int?>("Tenancy:InvitationExpirationHours")
            ?? throw new InvalidOperationException(
                "Required configuration 'Tenancy:InvitationExpirationHours' is missing.");

        if (validityHours <= 0)
            throw new InvalidOperationException(
                "Tenancy invitation expiration must be greater than zero.");

        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var invitation = new TenantInvitation
        {
            Email = NormalizeEmail(email),
            Role = role,
            TokenHash = TokenHasher.Hash(token),
            ExpiresAt = DateTime.UtcNow.AddHours(validityHours),
            InvitedByUserId = invitedByUserId
        };

        return (invitation, token);
    }

    private static string NormalizeEmail(string email)
    {
        var normalized = email.Trim().ToLowerInvariant();

        if (!MailAddress.TryCreate(normalized, out var parsed))
            throw new ArgumentException("A valid email address is required.", nameof(email));

        return parsed.Address.ToLowerInvariant();
    }
}
