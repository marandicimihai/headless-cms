using HeadlessCms.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace HeadlessCms.Api.Tests;

public sealed class PostgreSqlApp : ApiApp
{
    private readonly PostgreSqlContainer container =
        new PostgreSqlBuilder(
            Environment.GetEnvironmentVariable("TEST_POSTGRES_IMAGE")
            ?? "postgres:18-alpine")
        .Build();

    protected override async ValueTask PreSetupAsync()
    {
        await container.StartAsync();
    }

    protected override void ConfigureServices(IServiceCollection services)
    {
        services.RemoveAll<ApplicationDbContext>();
        services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
        services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
        services.AddDbContext<ApplicationDbContext>(
            options => options.UseNpgsql(container.GetConnectionString()));
    }

    protected override async ValueTask TearDownAsync()
    {
        await base.TearDownAsync();
        await container.DisposeAsync();
    }

    public override async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.ExecuteSqlRawAsync(
            """
            DROP SCHEMA IF EXISTS public CASCADE;
            CREATE SCHEMA public;
            """);
        await db.Database.MigrateAsync();
    }
}
