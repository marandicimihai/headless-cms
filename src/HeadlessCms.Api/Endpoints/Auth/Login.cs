using FastEndpoints.Security;
using FluentValidation;
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

public class Login(
    UserManager userManager,
    IConfiguration config
) : Endpoint<LoginRequest>
{
    public override void Configure()
    {
        Post("login");
        AllowAnonymous();
    }

    public override async Task HandleAsync(LoginRequest req, CancellationToken ct)
    {
        var username = req.Username;
        var password = req.Password;

        if (await userManager.CredentialsAreValid(username, password, ct))
        {
            var expirationMinutes =
                config.GetValue<int?>("Auth:JwtExpirationMinutes")
                ?? throw new InvalidOperationException(
                    "Required configuration 'Auth:JwtExpirationMinutes' is missing.");

            var jwt = JwtBearer.CreateToken(o =>
            {
                o.SigningKey = config["Auth:SigningKey"]!;
                o.ExpireAt = DateTime.UtcNow.AddMinutes(expirationMinutes);
                o.User["Username"] = username;
            });
            
            await Send.OkAsync(new
            {
                Token = jwt
            }, ct);
        }
        else
        {
            await Send.UnauthorizedAsync(ct);
        }
    }
}