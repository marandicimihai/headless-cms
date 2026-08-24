using FastEndpoints.Security;
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

public class Login(UserManager userManager) : Endpoint<LoginRequest, SessionTokens>
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
            Response = await CreateTokenWith<Refresh>(
                user!.Id,
                privileges =>
                {
                    privileges["sub"] = user.Id;
                    privileges["email"] = user.Email;
                    privileges.Roles.Add(user.PlatformRole.ToString());
                });
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
