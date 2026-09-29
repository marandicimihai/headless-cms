using FluentValidation;
using HeadlessCms.Api.Auth.Services;
using HeadlessCms.Api.Workspaces.Models;
using HeadlessCms.Api.Workspaces.Services;

using HeadlessCms.Api.Documentation;

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
        Description(b => b.WithTags("Authentication"), clearDefaults: true);
        Summary((EndpointSummary s) =>
        {
            s.Summary = "Register with an invitation";
            s.Description = "Access: Anonymous with a valid invitation for a new account.\n\nCreates the invited user and membership and sets cms_session. Password must contain 15–64 characters. Existing accounts must log in and accept the invitation instead.";
            s.Params["Token"] = "Single-use invitation token from the invitation link; maximum 512 characters.";
            s.Params["Password"] = "Password; new accounts require 15–64 characters.";
            s.ExampleRequest = new { Token = "EXAMPLE_TOKEN", Password = "example-password-123" };
            s.Response<RegisterWithInvitation.ResponseDto>(200, "Success.");
            s.ResponseExamples[200] = new { ApiExamples.UserId, Email = "editor@example.com", Membership = ApiExamples.Membership };
            s.Response<FastEndpoints.ProblemDetails>(400, "Invalid request or validation failure.", "application/problem+json");
            s.Response<ApiProblem>(404, "Resource not found or inaccessible.", "application/problem+json");
            s.Response<ApiProblem>(409, "Conflicting membership or invitation state.", "application/problem+json");
            s.Response<ApiProblem>(410, "Invitation expired or revoked.", "application/problem+json");
        });
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
