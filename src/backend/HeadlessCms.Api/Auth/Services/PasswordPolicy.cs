namespace HeadlessCms.Api.Auth.Services;

public static class PasswordPolicy
{
    public const int MinimumLength = 15;
    public const int MaximumLength = 64;

    public static string? Validate(string? password)
    {
        if (string.IsNullOrEmpty(password))
            return "Password is required.";

        if (password.Length < MinimumLength)
            return $"Password must be at least {MinimumLength} characters long.";

        if (password.Length > MaximumLength)
            return $"Password must be at most {MaximumLength} characters long.";

        if (password.All(char.IsWhiteSpace))
            return "Password cannot contain only whitespace.";

        return null;
    }
}
