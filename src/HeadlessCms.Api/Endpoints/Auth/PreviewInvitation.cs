using FastEndpoints;
using FluentValidation;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;

namespace HeadlessCms.Api.Endpoints.Auth;

public sealed class PreviewInvitation(WorkspaceInvitationService invitations)
    : Endpoint<PreviewInvitation.Request, PreviewInvitation.ResponseDto>
{
    public override void Configure()
    {
        Post("auth/invitations/preview");
        AllowAnonymous();
    }

    public override async Task HandleAsync(Request request, CancellationToken ct)
    {
        try
        {
            var invitation = await invitations.PreviewAsync(request.Token, ct);
            Response = new ResponseDto(
                invitation.WorkspaceName,
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

    public sealed class Request
    {
        public required string Token { get; init; }
    }

    public sealed record ResponseDto(
        string WorkspaceName,
        string MaskedEmail,
        WorkspaceRole Role,
        DateTime ExpiresAt);

    public sealed class RequestValidator : Validator<Request>
    {
        public RequestValidator() =>
            RuleFor(request => request.Token).NotEmpty().MaximumLength(512);
    }
}
