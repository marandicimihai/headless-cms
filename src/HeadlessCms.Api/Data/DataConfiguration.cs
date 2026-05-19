using HeadlessCms.Api.Auth.Models;
using Microsoft.EntityFrameworkCore;

namespace HeadlessCms.Api.Data;

public static class DataConfiguration
{
    public static void ConfigureDataServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"));
            });
    }

    public static async Task SeedData(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var devUserEmail = "dev@headlesscms.local";

            if (!await db.Users.AnyAsync(u => u.Email == devUserEmail))
            {
                db.Users.Add(new User { Email = devUserEmail });
                await db.SaveChangesAsync();
            }
        }
    }
}