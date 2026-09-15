using FluentValidation;
using HeadlessCms.Api.Auth.Services;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;

namespace HeadlessCms.Api.Endpoints.Auth;

public sealed class RegisterWithInvitation(
    WorkspaceInvitationService invitations,
    AuthSessionService sessions)
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
            var created = await sessions.CreateAsync(user.Id, ct);
            sessions.AppendCookie(HttpContext.Response, created);

            Response = new ResponseDto(
                user.Id,
                user.Email,
                new Membership(
                    registration.Workspace.Id,
                    registration.Workspace.Name,
                    registration.Membership.Role,
                    registration.Membership.JoinedAt));
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
        Membership Membership);

    public sealed class RequestValidator : Validator<Request>
    {
        public RequestValidator()
        {
            RuleFor(request => request.Token).NotEmpty().MaximumLength(512);
            RuleFor(request => request.Password)
                .Must(password => PasswordPolicy.Validate(password) is null)
                .WithMessage(request => PasswordPolicy.Validate(request.Password) ?? string.Empty);
        }
    }
}
