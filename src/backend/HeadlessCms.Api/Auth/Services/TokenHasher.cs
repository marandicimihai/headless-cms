namespace HeadlessCms.Api.Auth.Services;

using System.Security.Cryptography;
using System.Text;

public static class TokenHasher
{
    public static string Hash(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);

        return Convert.ToHexString(hash);
    }
}