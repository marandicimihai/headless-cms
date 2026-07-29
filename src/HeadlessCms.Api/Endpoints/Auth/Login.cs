using FastEndpoints.Security;
using FluentValidation;
using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Auth.Services;

namespace HeadlessCms.Api.Endpoints.Auth;

public class LoginRequest
{
    public required string Username { get; init; }
    public required string Password { get; init; }

    public class LoginRequestValidator : Validator<LoginRequest>
    {
        public LoginRequestValidator()
        {
            RuleFor(request => request.Username)
                .MaximumLength(64)
                .NotEmpty();
            
            RuleFor(request => request.Password)
                .MaximumLength(64)
                .NotEmpty();
        }
    }
}

public class Login(UserManager userManager) : Endpoint<LoginRequest, TokenResponse>
{
    public override void Configure()
    {
        Post("auth/login");
        AllowAnonymous();
    }

    public override async Task HandleAsync(LoginRequest req, CancellationToken ct)
    {
        var username = req.Username;
        var password = req.Password;

        var (valid, user) = await userManager.CredentialsAreValidWithUser(username, password, ct);
        
        if (valid)
        {
            Response = await CreateTokenWith<Refresh>(
                user!.Id,
                privileges =>
                {
                    privileges["sub"] = user.Id;
                    privileges["username"] = user.Username;
                    privileges.Roles.Add(user.PlatformRole.ToString());
                });
        }
        else
        {
            await Send.UnauthorizedAsync(ct);
        }
    }
}
