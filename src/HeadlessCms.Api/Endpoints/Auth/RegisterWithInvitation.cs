using FastEndpoints;
using FastEndpoints.Security;
using FluentValidation;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;

namespace HeadlessCms.Api.Endpoints.Auth;

public sealed class RegisterWithInvitation(
    WorkspaceInvitationService invitations,
    Refresh tokenService)
    : Endpoint<RegisterWithInvitation.Request, RegisterWithInvitation.ResponseDto>
{
    public override void Configure()
    {
        Post("auth/invitations/register");
        AllowAnonymous();
    }

    public override async Task HandleAsync(Request request, CancellationToken ct)
    {
        try
        {
            var registration =
                await invitations.RegisterAsync(request.Token, request.Password, ct);
            var user = registration.User;
            var tokens = await tokenService.CreateCustomToken(
                user.Id,
                privileges =>
                {
                    privileges["sub"] = user.Id;
                    privileges["email"] = user.Email;
                    privileges.Roles.Add(user.PlatformRole.ToString());
                },
                response => response,
                false,
                request);

            Response = new ResponseDto(
                user.Id,
                user.Email,
                new Membership(
                    registration.Workspace.Id,
                    registration.Workspace.Name,
                    registration.Membership.Role,
                    registration.Membership.JoinedAt),
                tokens);
        }
        catch (InvitationFlowException exception)
        {
            await ApiErrors.SendAsync(HttpContext, exception, ct);
        }
    }

    public sealed class Request
    {
        public required string Token { get; init; }
        public required string Password { get; init; }
    }

    public sealed record Membership(
        Guid WorkspaceId,
        string WorkspaceName,
        WorkspaceRole Role,
        DateTime JoinedAt);

    public sealed record ResponseDto(
        string UserId,
        string Email,
        Membership Membership,
        TokenResponse Tokens);

    public sealed class RequestValidator : Validator<Request>
    {
        public RequestValidator()
        {
            RuleFor(request => request.Token).NotEmpty().MaximumLength(512);
            RuleFor(request => request.Password).NotEmpty().MaximumLength(64);
        }
    }
}
