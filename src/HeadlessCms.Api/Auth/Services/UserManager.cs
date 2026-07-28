using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Auth.Services;

public class UserManager(
    ApplicationDbContext db,
    IPasswordHasher<User> hasher)
{
    public async Task<bool> CredentialsAreValid(string username, string password, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Username == username, ct);

        if (user is null)
            return false;
        
        return hasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;
    }
}