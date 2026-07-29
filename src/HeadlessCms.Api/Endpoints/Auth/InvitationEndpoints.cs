using FastEndpoints;
using FastEndpoints.Security;
using HeadlessCms.Api.Tenancy.Services;

namespace HeadlessCms.Api.Endpoints.Auth;

public sealed class PreviewInvitation(TenantInvitationService invitations)
    : Endpoint<InvitationTokenRequest, InvitationPreviewResponse>
{
    public override void Configure()
    {
        Post("auth/invitations/preview");
        AllowAnonymous();
    }

    public override async Task HandleAsync(InvitationTokenRequest request, CancellationToken ct)
    {
        try
        {
            var invitation = await invitations.PreviewAsync(request.Token, ct);
            Response = new InvitationPreviewResponse(
                invitation.TenantName,
                Mask(invitation.Email),
                invitation.Role,
                invitation.ExpiresAt);
        }
        catch (InvitationFlowException exception)
        {
            await ApiErrors.SendAsync(HttpContext, exception, ct);
        }
    }

    private static string Mask(string email)
    {
        var separator = email.IndexOf('@');
        var local = email[..separator];
        var maskedLocal = local.Length <= 1
            ? "*"
            : $"{local[0]}{new string('*', Math.Min(local.Length - 1, 6))}";
        return $"{maskedLocal}{email[separator..]}";
    }
}

public sealed class RegisterWithInvitation(
    TenantInvitationService invitations,
    Refresh refresh)
    : Endpoint<InvitationRegistrationRequest, InvitationRegistrationResponse>
{
    public override void Configure()
    {
        Post("auth/invitations/register");
        AllowAnonymous();
    }

    public override async Task HandleAsync(
        InvitationRegistrationRequest request,
        CancellationToken ct)
    {
        try
        {
            var registration =
                await invitations.RegisterAsync(request.Token, request.Password, ct);
            var user = registration.User;
            var tokens = await refresh.CreateInitialTokenAsync(
                user.Id,
                privileges =>
                {
                    privileges["sub"] = user.Id;
                    privileges["email"] = user.Email;
                    privileges.Roles.Add(user.PlatformRole.ToString());
                },
                request);

            Response = new InvitationRegistrationResponse(
                user.Id,
                user.Email,
                new MembershipResponse(
                    registration.Tenant.Id,
                    registration.Tenant.Name,
                    registration.Membership.Role,
                    registration.Membership.JoinedAt),
                tokens);
        }
        catch (InvitationFlowException exception)
        {
            await ApiErrors.SendAsync(HttpContext, exception, ct);
        }
    }
}

public sealed class AcceptInvitation(TenantInvitationService invitations)
    : Endpoint<InvitationTokenRequest, MembershipResponse>
{
    public override void Configure()
    {
        Post("auth/invitations/accept");
        Claims("sub");
    }

    public override async Task HandleAsync(InvitationTokenRequest request, CancellationToken ct)
    {
        try
        {
            var membership = await invitations.AcceptInvitationAsync(
                User.ClaimValue("sub")!,
                request.Token,
                ct);
            var tenantName = membership.Tenant?.Name;
            Response = new MembershipResponse(
                membership.TenantId,
                tenantName ?? string.Empty,
                membership.Role,
                membership.JoinedAt);
        }
        catch (InvitationFlowException exception)
        {
            await ApiErrors.SendAsync(HttpContext, exception, ct);
        }
    }
}
