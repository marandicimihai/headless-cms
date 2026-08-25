using FastEndpoints;
using FluentValidation;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Auth.Services;

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
                .MaximumLength(64)
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
