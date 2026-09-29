using FastEndpoints;
using FluentValidation;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Auth.Services;

using HeadlessCms.Api.Documentation;

namespace HeadlessCms.Api.Endpoints.Auth;

public class LoginRequest
{
    public required string Email { get; init; }
    public required string Password { get; init; }

    public class LoginRequestValidator : Validator<LoginRequest>
    {
        public LoginRequestValidator()
        {
            RuleFor(request => request.Email)
                .EmailAddress()
                .MaximumLength(320)
                .NotEmpty();

            RuleFor(request => request.Password)
                .MaximumLength(PasswordPolicy.MaximumLength)
                .NotEmpty();
        }
    }
}

public class Login(
    UserManager userManager,
    AuthSessionService sessions) : Endpoint<LoginRequest, AuthSessionResponse>
{
    public override void Configure()
    {
        Post("auth/login");
        AllowAnonymous();
        Description(b => b.WithTags("Authentication"), clearDefaults: true);
        Summary((EndpointSummary s) =>
        {
            s.Summary = "Log in";
            s.Description = "Access: Anonymous.\n\nValidates email and password and sets the HttpOnly cms_session cookie. Reuse the cookie jar for subsequent requests. No session secret is returned in JSON.";
            s.Params["Email"] = "Email address (maximum 320 characters).";
            s.Params["Password"] = "Password; new accounts require 15–64 characters.";
            s.ExampleRequest = new { Email = "editor@example.com", Password = "example-password-123" };
            s.Response<AuthSessionResponse>(200, "Success.");
            s.ResponseExamples[200] = ApiExamples.Session;
            s.Response<FastEndpoints.ProblemDetails>(400, "Invalid request or validation failure.", "application/problem+json");
            s.Response<ApiProblem>(401, "The email or password is incorrect (invalid_credentials).", "application/problem+json");
        });
    }

    public override async Task HandleAsync(LoginRequest req, CancellationToken ct)
    {
        var (valid, user) =
            await userManager.CredentialsAreValidWithUser(req.Email, req.Password, ct);

        if (valid)
        {
            var created = await sessions.CreateAsync(user!.Id, ct);
            sessions.AppendCookie(HttpContext.Response, created);
            Response = new AuthSessionResponse(
                user.Id,
                user.Email,
                user.PlatformRole,
                created.Session.IdleExpiresAt,
                created.Session.AbsoluteExpiresAt);
        }
        else
        {
            await ApiErrors.SendAsync(
                HttpContext,
                StatusCodes.Status401Unauthorized,
                "invalid_credentials",
                "The email or password is incorrect.",
                ct);
        }
    }
}
