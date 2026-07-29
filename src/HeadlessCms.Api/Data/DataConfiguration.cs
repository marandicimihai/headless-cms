using HeadlessCms.Api.Auth.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Data;

public static class DataConfiguration
{
    public static async Task SeedPlatformAdminUser(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            var username = app.Configuration["Auth:AdminUsername"] ??
                           throw new InvalidOperationException("Auth:AdminUsername not configured");

            var admin = await db.Users.SingleOrDefaultAsync(user => user.Username == username);

            if (admin is null)
            {
                var password = app.Configuration["Auth:AdminPassword"] ??
                               throw new InvalidOperationException("Auth:AdminPassword not configured");

                var hasher = new PasswordHasher<User>();

                admin = new User
                {
                    Username = username,
                    PlatformRole = PlatformRole.PlatformAdmin
                };
                
                admin.PasswordHash = hasher.HashPassword(admin, password);
                    
                db.Users.Add(admin);
                await db.SaveChangesAsync();
            }
            else if (admin.PlatformRole != PlatformRole.PlatformAdmin)
            {
                admin.PlatformRole = PlatformRole.PlatformAdmin;
                await db.SaveChangesAsync();
            }
        }
    }
}
