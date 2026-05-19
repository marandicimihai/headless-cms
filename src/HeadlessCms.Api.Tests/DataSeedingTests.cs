using HeadlessCms.Api.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HeadlessCms.Api.Tests;

[TestClass]
public sealed class DataSeedingTests
{
    [TestMethod]
    public async Task SeedData_DoesNotCreateDevUser_WhenEnvironmentIsNotDevelopment()
    {
        await using var app = BuildApp(Environments.Production);

        await app.SeedData();

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var hasDevUser = await db.Users.AnyAsync(u => u.Email == "dev@headlesscms.local");

        Assert.IsFalse(hasDevUser);
    }

    [TestMethod]
    public async Task SeedData_CreatesDevUser_WhenEnvironmentIsDevelopment()
    {
        await using var app = BuildApp(Environments.Development);

        await app.SeedData();

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var hasDevUser = await db.Users.AnyAsync(u => u.Email == "dev@headlesscms.local");

        Assert.IsTrue(hasDevUser);
    }

    private static WebApplication BuildApp(string environmentName)
    {
        var options = new WebApplicationOptions { EnvironmentName = environmentName };
        var builder = WebApplication.CreateBuilder(options);
        var databaseRoot = new InMemoryDatabaseRoot();
        var databaseName = $"headless-cms-tests-{Guid.NewGuid()}";

        builder.Services.AddDbContext<ApplicationDbContext>(db =>
            db.UseInMemoryDatabase(databaseName, databaseRoot));

        return builder.Build();
    }
}