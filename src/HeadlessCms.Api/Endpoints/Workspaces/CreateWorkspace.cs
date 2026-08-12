using FastEndpoints;
using FastEndpoints.Security;
using FluentValidation;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Endpoints.Workspaces;

public sealed class CreateWorkspace(
    WorkspaceInvitationService invitations,
    ApplicationDbContext db)
    : Endpoint<CreateWorkspaceRequest, CreateWorkspaceResponse>
{
    public override void Configure()
    {
        Post("workspaces");
        Roles(nameof(PlatformRole.PlatformAdmin));
    }

    public override async Task HandleAsync(CreateWorkspaceRequest request, CancellationToken ct)
    {
        try
        {
            var created = await invitations.CreateWorkspaceWithOwnerInvitationAsync(
                User.ClaimValue("sub")!,
                request.Name,
                request.OwnerEmail,
                ct);
            var workspaceEntity = await db.Workspaces
                .AsNoTracking()
                .SingleAsync(workspace => workspace.Id == created.WorkspaceId, ct);
            var invitationEntity = await db.WorkspaceInvitations
                .AsNoTracking()
                .SingleAsync(invitation => invitation.Id == created.InvitationId, ct);
            var workspace = new CreateWorkspaceWorkspaceResponse(
                workspaceEntity.Id,
                workspaceEntity.Name,
                workspaceEntity.CreatedAt);

            await Send.ResponseAsync(
                new CreateWorkspaceResponse(workspace, ToResponse(invitationEntity)),
                StatusCodes.Status201Created,
                ct);
        }
        catch (InvitationFlowException exception)
        {
            await ApiErrors.SendAsync(HttpContext, exception, ct);
        }
    }

    private static CreateWorkspaceOwnerInvitationResponse ToResponse(
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

public sealed class CreateWorkspaceRequest
{
    public required string Name { get; init; }
    public required string OwnerEmail { get; init; }
}

public sealed class CreateWorkspaceRequestValidator : Validator<CreateWorkspaceRequest>
{
    public CreateWorkspaceRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(100);
        RuleFor(request => request.OwnerEmail).NotEmpty().EmailAddress().MaximumLength(320);
    }
}

public sealed record CreateWorkspaceWorkspaceResponse(
    Guid Id,
    string Name,
    DateTime CreatedAt,
    WorkspaceRole? CurrentRole = null);

public sealed record CreateWorkspaceOwnerInvitationResponse(
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

public sealed record CreateWorkspaceResponse(
    CreateWorkspaceWorkspaceResponse Workspace,
    CreateWorkspaceOwnerInvitationResponse OwnerInvitation);
