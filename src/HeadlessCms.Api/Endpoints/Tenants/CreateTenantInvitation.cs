using FastEndpoints;
using FastEndpoints.Security;
using FluentValidation;
using HeadlessCms.Api.Data;
using HeadlessCms.Api.Tenancy.Models;
using HeadlessCms.Api.Tenancy.Services;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Endpoints.Tenants;

public sealed class CreateTenantInvitation(
    TenantInvitationService invitations,
    ApplicationDbContext db)
    : Endpoint<CreateTenantInvitationRequest, CreateTenantInvitationResponse>
{
    public override void Configure()
    {
        Post("tenants/{tenantId:guid}/invitations");
        Claims("sub");
    }

    public override async Task HandleAsync(
        CreateTenantInvitationRequest request,
        CancellationToken ct)
    {
        try
        {
            var created = await invitations.CreateInvitationAsync(
                User.ClaimValue("sub")!,
                request.TenantId,
                request.Email,
                request.Role,
                ct);
            var invitation = await db.TenantInvitations
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

    private static CreateTenantInvitationResponse ToResponse(
        TenantInvitation invitation) =>
        new(
            invitation.Id,
            invitation.TenantId,
            invitation.Email,
            invitation.Role,
            TenantInvitationService.GetStatus(invitation),
            invitation.CreatedAt,
            invitation.ExpiresAt,
            invitation.LastSentAt,
            invitation.AcceptedAt,
            invitation.RevokedAt);
}

public sealed class CreateTenantInvitationRequest
{
    public Guid TenantId { get; init; }
    public required string Email { get; init; }
    public TenantRole Role { get; init; }
}

public sealed class CreateTenantInvitationRequestValidator
    : Validator<CreateTenantInvitationRequest>
{
    public CreateTenantInvitationRequestValidator()
    {
        RuleFor(request => request.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(request => request.Role)
            .Must(role => role is TenantRole.Editor or TenantRole.Member)
            .WithMessage("Role must be Editor or Member.");
    }
}

public sealed record CreateTenantInvitationResponse(
    Guid Id,
    Guid TenantId,
    string Email,
    TenantRole Role,
    InvitationStatus Status,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    DateTime? LastSentAt,
    DateTime? AcceptedAt,
    DateTime? RevokedAt);
