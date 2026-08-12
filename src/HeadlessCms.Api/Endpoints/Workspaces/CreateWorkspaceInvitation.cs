using FastEndpoints;
using FastEndpoints.Security;
using FluentValidation;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Endpoints.Workspaces;

public sealed class CreateWorkspaceInvitation(
    WorkspaceInvitationService invitations,
    ApplicationDbContext db)
    : Endpoint<CreateWorkspaceInvitationRequest, CreateWorkspaceInvitationResponse>
{
    public override void Configure()
    {
        Post("workspaces/{workspaceId:guid}/invitations");
        Claims("sub");
    }

    public override async Task HandleAsync(
        CreateWorkspaceInvitationRequest request,
        CancellationToken ct)
    {
        try
        {
            var created = await invitations.CreateInvitationAsync(
                User.ClaimValue("sub")!,
                request.WorkspaceId,
                request.Email,
                request.Role,
                ct);
            var invitation = await db.WorkspaceInvitations
                .AsNoTracking()
                .SingleAsync(candidate => candidate.Id == created.InvitationId, ct);
            await Send.ResponseAsync(
                ToResponse(invitation),
                StatusCodes.Status201Created,
                ct);
        }
        catch (InvitationFlowException exception)
        {
            await ApiErrors.SendAsync(HttpContext, exception, ct);
        }
    }

    private static CreateWorkspaceInvitationResponse ToResponse(
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

public sealed class CreateWorkspaceInvitationRequest
{
    public Guid WorkspaceId { get; init; }
    public required string Email { get; init; }
    public WorkspaceRole Role { get; init; }
}

public sealed class CreateWorkspaceInvitationRequestValidator
    : Validator<CreateWorkspaceInvitationRequest>
{
    public CreateWorkspaceInvitationRequestValidator()
    {
        RuleFor(request => request.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(request => request.Role)
            .Must(role => role is WorkspaceRole.Editor or WorkspaceRole.Member)
            .WithMessage("Role must be Editor or Member.");
    }
}

public sealed record CreateWorkspaceInvitationResponse(
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
