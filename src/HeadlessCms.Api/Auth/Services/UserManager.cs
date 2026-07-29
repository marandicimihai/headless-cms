using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Auth.Services;

public class UserManager(
    ApplicationDbContext db,
    IPasswordHasher<User> hasher)
{
    public async Task<(bool, User?)> CredentialsAreValidWithUser(
        string email,
        string password,
        CancellationToken ct)
    {
        string normalizedEmail;
        try
        {
            normalizedEmail = EmailNormalizer.Normalize(email);
        }
        catch (ArgumentException)
        {
            return (false, null);
        }

        var user = await db.Users.SingleOrDefaultAsync(
            candidate => candidate.Email == normalizedEmail,
            ct);

        if (user is null)
            return (false, null);
        
        return (hasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed, user);
    }
}
