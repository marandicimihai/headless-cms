using HeadlessCms.Api.Auth.Models;
using HeadlessCms.Api.Auth.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Data;

public static class DataConfiguration
{
    public static async Task SeedPlatformAdminUser(this WebApplication app)
    {
        if (app.Environment.IsEnvironment("Testing"))
            return;

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var email = EmailNormalizer.Normalize(
            app.Configuration["Auth:AdminEmail"] ??
            throw new InvalidOperationException("Auth:AdminEmail not configured"));
        var legacyUsername = app.Configuration["Auth:LegacyAdminUsername"];
        var legacyEmail = legacyUsername is null
            ? null
            : $"{legacyUsername.Trim().ToLowerInvariant()}@legacy.invalid";

        var admin = await db.Users.SingleOrDefaultAsync(
            user =>
                user.PlatformRole == PlatformRole.PlatformAdmin ||
                user.Email == email ||
                (legacyEmail != null && user.Email == legacyEmail));

        if (admin is null)
        {
            var password = app.Configuration["Auth:AdminPassword"] ??
                           throw new InvalidOperationException("Auth:AdminPassword not configured");

            var hasher = new PasswordHasher<User>();
            admin = new User
            {
                Email = email,
                PlatformRole = PlatformRole.PlatformAdmin
            };
            admin.PasswordHash = hasher.HashPassword(admin, password);
            db.Users.Add(admin);
        }
        else
        {
            admin.Email = email;
            admin.PlatformRole = PlatformRole.PlatformAdmin;
        }

        await db.SaveChangesAsync();
    }
}
