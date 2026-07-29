using FluentValidation;
using HeadlessCms.Api.Tenancy.Models;
using HeadlessCms.Api.Tenancy.Services;

namespace HeadlessCms.Api.Endpoints.Tenants;

public class TenantIdRequest
{
    public Guid TenantId { get; init; }
}

public sealed class TenantPageRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed class TenantResourceRequest : TenantIdRequest;

public sealed class CreateTenantRequest
{
    public required string Name { get; init; }
    public required string OwnerEmail { get; init; }
}

public sealed class RenameTenantRequest : TenantIdRequest
{
    public required string Name { get; init; }
}

public sealed record TenantResponse(
    Guid Id,
    string Name,
    DateTime CreatedAt,
    TenantRole? CurrentRole = null);

public sealed record InvitationResponse(
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

public sealed record CreateTenantResponse(
    TenantResponse Tenant,
    InvitationResponse OwnerInvitation);

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int Total);

public sealed class InvitationPageRequest : TenantIdRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public InvitationStatus? Status { get; init; }
}

public sealed class InvitationResourceRequest : TenantIdRequest
{
    public Guid InvitationId { get; init; }
}

public sealed class CreateInvitationRequest : TenantIdRequest
{
    public required string Email { get; init; }
    public TenantRole Role { get; init; }
}

public sealed record MemberResponse(
    string UserId,
    string Email,
    TenantRole Role,
    DateTime JoinedAt);

public sealed class MemberPageRequest : TenantIdRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public class MemberResourceRequest : TenantIdRequest
{
    public string UserId { get; init; } = default!;
}

public sealed class ChangeMemberRoleRequest : MemberResourceRequest
{
    public TenantRole Role { get; init; }
}

public sealed class TransferOwnershipRequest : TenantIdRequest
{
    public required string NewOwnerUserId { get; init; }
}

public sealed class CreateTenantRequestValidator : Validator<CreateTenantRequest>
{
    public CreateTenantRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(100);
        RuleFor(request => request.OwnerEmail).NotEmpty().EmailAddress().MaximumLength(320);
    }
}

public sealed class RenameTenantRequestValidator : Validator<RenameTenantRequest>
{
    public RenameTenantRequestValidator() =>
        RuleFor(request => request.Name).NotEmpty().MaximumLength(100);
}

public sealed class CreateInvitationRequestValidator : Validator<CreateInvitationRequest>
{
    public CreateInvitationRequestValidator()
    {
        RuleFor(request => request.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(request => request.Role)
            .Must(role => role is TenantRole.Editor or TenantRole.Member)
            .WithMessage("Role must be Editor or Member.");
    }
}

internal static class TenantMappings
{
    public static InvitationResponse ToResponse(this TenantInvitation invitation) =>
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
