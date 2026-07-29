using FastEndpoints.Security;
using FluentValidation;
using HeadlessCms.Api.Tenancy.Models;

namespace HeadlessCms.Api.Endpoints.Auth;

public class InvitationTokenRequest
{
    public required string Token { get; init; }
}

public sealed class InvitationRegistrationRequest : InvitationTokenRequest
{
    public required string Password { get; init; }
}

public sealed record InvitationPreviewResponse(
    string TenantName,
    string MaskedEmail,
    TenantRole Role,
    DateTime ExpiresAt);

public sealed record MembershipResponse(
    Guid TenantId,
    string TenantName,
    TenantRole Role,
    DateTime JoinedAt);

public sealed record InvitationRegistrationResponse(
    string UserId,
    string Email,
    MembershipResponse Membership,
    TokenResponse Tokens);

public sealed class InvitationTokenRequestValidator : Validator<InvitationTokenRequest>
{
    public InvitationTokenRequestValidator() =>
        RuleFor(request => request.Token).NotEmpty().MaximumLength(512);
}

public sealed class InvitationRegistrationRequestValidator
    : Validator<InvitationRegistrationRequest>
{
    public InvitationRegistrationRequestValidator()
    {
        RuleFor(request => request.Token).NotEmpty().MaximumLength(512);
        RuleFor(request => request.Password).NotEmpty().MaximumLength(64);
    }
}
