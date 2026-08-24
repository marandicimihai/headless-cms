using System.Security.Cryptography;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Auth.Services;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Workspaces.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Workspaces.Services;

public sealed record CreatedWorkspaceInvitation(
    Guid WorkspaceId,
    Guid InvitationId,
    string Token,
    DateTime ExpiresAt);

public sealed record InvitationPreview(
    Guid InvitationId,
    Guid WorkspaceId,
    string WorkspaceName,
    string Email,
    WorkspaceRole Role,
    DateTime ExpiresAt,
    InvitationStatus Status);

public sealed record InvitationRegistration(User User, WorkspaceMembership Membership, Workspace Workspace);

public sealed class InvitationFlowException(
    int statusCode,
    string code,
    string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
}

public class WorkspaceInvitationService(
    ApplicationDbContext db,
    IConfiguration configuration,
    IPasswordHasher<User> passwordHasher,
    IInvitationEmailSender emailSender)
{
    public async Task<CreatedWorkspaceInvitation> CreateInvitationAsync(
        string ownerUserId,
        Guid workspaceId,
        string email,
        WorkspaceRole role,
        CancellationToken ct = default)
    {
        if (role == WorkspaceRole.Owner)
            throw BadRequest("Workspace owners can invite editors or members.");

        var workspace = await db.Workspaces.SingleOrDefaultAsync(candidate => candidate.Id == workspaceId, ct)
                     ?? throw NotFound("Workspace not found.");

        var ownerMembership = await db.WorkspaceMemberships.AnyAsync(
            membership =>
                membership.UserId == ownerUserId &&
                membership.WorkspaceId == workspaceId &&
                membership.Role == WorkspaceRole.Owner,
            ct);

        if (!ownerMembership)
            throw Forbidden("Only a workspace owner can invite users.");

        var normalizedEmail = EmailNormalizer.Normalize(email);
        var alreadyMember = await db.WorkspaceMemberships.AnyAsync(
            membership =>
                membership.WorkspaceId == workspaceId &&
                membership.User.Email == normalizedEmail,
            ct);

        if (alreadyMember)
            throw Conflict("The user is already a member of this workspace.");

        var pendingInvitation = await db.WorkspaceInvitations.AnyAsync(
            invitation =>
                invitation.WorkspaceId == workspaceId &&
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
        invitation.WorkspaceId = workspaceId;

        db.WorkspaceInvitations.Add(invitation);
        await db.SaveChangesAsync(ct);
        await SendInvitationAsync(invitation, workspace.Name, token, ct);

        return new CreatedWorkspaceInvitation(
            workspaceId,
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
            invitation.WorkspaceId,
            invitation.Workspace.Name,
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

        var membership = new WorkspaceMembership
        {
            WorkspaceId = invitation.WorkspaceId,
            User = user,
            Role = invitation.Role
        };

        invitation.AcceptedAt = DateTime.UtcNow;
        invitation.AcceptedByUser = user;
        db.Users.Add(user);
        db.WorkspaceMemberships.Add(membership);
        await db.SaveChangesAsync(ct);

        return new InvitationRegistration(user, membership, invitation.Workspace);
    }

    public async Task<WorkspaceMembership> AcceptInvitationAsync(
        string userId,
        string token,
        CancellationToken ct = default)
    {
        var invitation = await FindActiveByTokenAsync(token, ct);
        var user = await db.Users.SingleOrDefaultAsync(candidate => candidate.Id == userId, ct)
                   ?? throw NotFound("User not found.");

        if (!string.Equals(user.Email, invitation.Email, StringComparison.Ordinal))
            throw Forbidden("The invitation belongs to a different email address.");

        if (await db.WorkspaceMemberships.AnyAsync(
                membership =>
                    membership.WorkspaceId == invitation.WorkspaceId &&
                    membership.UserId == userId,
                ct))
        {
            throw Conflict("The user is already a member of this workspace.");
        }

        var membership = new WorkspaceMembership
        {
            WorkspaceId = invitation.WorkspaceId,
            Workspace = invitation.Workspace,
            UserId = userId,
            Role = invitation.Role
        };

        invitation.AcceptedAt = DateTime.UtcNow;
        invitation.AcceptedByUserId = userId;
        db.WorkspaceMemberships.Add(membership);
        await db.SaveChangesAsync(ct);

        return membership;
    }

    public async Task<CreatedWorkspaceInvitation> ResendAsync(
        WorkspaceInvitation invitation,
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

        var workspaceName = await db.Workspaces
            .Where(workspace => workspace.Id == invitation.WorkspaceId)
            .Select(workspace => workspace.Name)
            .SingleAsync(ct);
        await SendInvitationAsync(invitation, workspaceName, token, ct);

        return new CreatedWorkspaceInvitation(
            invitation.WorkspaceId,
            invitation.Id,
            token,
            invitation.ExpiresAt);
    }

    public async Task RevokeAsync(WorkspaceInvitation invitation, CancellationToken ct = default)
    {
        if (invitation.AcceptedAt is not null)
            throw Conflict("Accepted invitations cannot be revoked.");

        invitation.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public static InvitationStatus GetStatus(WorkspaceInvitation invitation)
    {
        if (invitation.AcceptedAt is not null)
            return InvitationStatus.Accepted;
        if (invitation.RevokedAt is not null)
            return InvitationStatus.Revoked;
        return DateTime.UtcNow >= invitation.ExpiresAt
            ? InvitationStatus.Expired
            : InvitationStatus.Pending;
    }

    private async Task<WorkspaceInvitation> FindByTokenAsync(
        string token,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw NotFound("Invitation not found.");

        var hash = TokenHasher.Hash(token);
        return await db.WorkspaceInvitations
                   .Include(invitation => invitation.Workspace)
                   .SingleOrDefaultAsync(invitation => invitation.TokenHash == hash, ct)
               ?? throw NotFound("Invitation not found.");
    }

    private async Task<WorkspaceInvitation> FindActiveByTokenAsync(
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

    private (WorkspaceInvitation Invitation, string Token) CreateInvitationEntity(
        string email,
        WorkspaceRole role,
        string invitedByUserId)
    {
        var token = CreateToken();
        var invitation = new WorkspaceInvitation
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
        WorkspaceInvitation invitation,
        string workspaceName,
        string token,
        CancellationToken ct)
    {
        await emailSender.SendAsync(
            invitation.Email,
            workspaceName,
            invitation.Role,
            token,
            ct);
        invitation.LastSentAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private int GetValidityHours()
    {
        var hours = configuration.GetValue<int?>("Workspaces:InvitationExpirationHours")
                    ?? throw new InvalidOperationException(
                        "Required configuration 'Workspaces:InvitationExpirationHours' is missing.");

        return hours > 0
            ? hours
            : throw new InvalidOperationException(
                "Workspaces invitation expiration must be greater than zero.");
    }

    private static string CreateToken() =>
        WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

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
