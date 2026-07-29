using System.Net.Mail;

namespace HeadlessCms.Api.Auth.Services;

public static class EmailNormalizer
{
    public static string Normalize(string email)
    {
        var normalized = email.Trim().ToLowerInvariant();

        if (!MailAddress.TryCreate(normalized, out var parsed))
            throw new ArgumentException("A valid email address is required.", nameof(email));

        return parsed.Address.ToLowerInvariant();
    }
}
