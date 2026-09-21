using HeadlessCms.Api.Auth.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HeadlessCms.Api.Data;

public static class DatabaseInitializer
{
    public static async Task RunAsync(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");
        var email = builder.Configuration["Auth:AdminEmail"];
        if (string.IsNullOrWhiteSpace(email) || !System.Net.Mail.MailAddress.TryCreate(email, out _))
            throw new InvalidOperationException("Auth:AdminEmail must be a valid email address.");
        var password = builder.Configuration["Auth:AdminPassword"];
        if (string.IsNullOrWhiteSpace(password) || password.Length > PasswordPolicy.MaximumLength ||
            (!builder.Environment.IsDevelopment() && PasswordPolicy.Validate(password) is not null))
            throw new InvalidOperationException("Auth:AdminPassword must be 15–64 characters outside development.");

        builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseNpgsql(connectionString));
        await using var app = builder.Build();
        await InitializeAsync(app, connectionString);
    }

    public static async Task InitializeAsync(WebApplication app, string connectionString)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var ct = timeout.Token;
        // A dedicated, non-pooled connection guarantees the session lock is released on disposal.
        var settings = new NpgsqlConnectionStringBuilder(connectionString) { Pooling = false, Timeout = 5 };
        await using var connection = new NpgsqlConnection(settings.ConnectionString);
        for (var attempt = 1; ; attempt++)
        {
            try { await connection.OpenAsync(ct); break; }
            catch (NpgsqlException exception) when (exception.IsTransient && attempt < 15)
            {
                app.Logger.LogWarning("Database not ready; initialization connection attempt {Attempt}/15", attempt);
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
        }
        await using var command = new NpgsqlCommand("SELECT pg_advisory_lock(731947202601)", connection)
        {
            CommandTimeout = 240
        };
        await command.ExecuteNonQueryAsync(ct);
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync(ct);
        await app.SeedPlatformAdminUser();
        app.Logger.LogInformation("Database initialized; migrations and administrator are ready");
    }
}
