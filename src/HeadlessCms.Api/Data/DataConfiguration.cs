using HeadlessCms.Api.Auth.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Data;

public static class DataConfiguration
{
    public static async Task SeedAdminUser(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            var username = app.Configuration["Auth:AdminUsername"] ??
                           throw new InvalidOperationException("Auth:AdminUsername not configured");

            if (!await db.Users.AnyAsync(u => u.Username == username))
            {
                var password = app.Configuration["Auth:AdminPassword"] ??
                               throw new InvalidOperationException("Auth:AdminPassword not configured");

                var hasher = new PasswordHasher<User>();

                var admin = new User { Username = username };
                
                admin.PasswordHash = hasher.HashPassword(admin, password);
                    
                db.Users.Add(admin);
                await db.SaveChangesAsync();
            }
        }
    }
}