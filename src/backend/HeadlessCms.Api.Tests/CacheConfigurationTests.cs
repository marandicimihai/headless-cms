using HeadlessCms.Api.Caching;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace HeadlessCms.Api.Tests;

public sealed class CacheConfigurationTests
{
    [Fact]
    public void Enabled_cache_requires_redis_configuration()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Caching:Enabled"] = "true",
            ["ConnectionStrings:Redis"] = ""
        });
        Should.Throw<InvalidOperationException>(() => builder.AddResourceCache());
    }

    [Theory]
    [InlineData("0", "valid")]
    [InlineData("30", "invalid{tag}")]
    public async Task Invalid_cache_options_fail_startup(string expiry, string prefix)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Caching:Enabled"] = "false",
            ["Caching:ExpirationSeconds"] = expiry,
            ["Caching:KeyPrefix"] = prefix
        });
        builder.AddResourceCache();
        await using var app = builder.Build();
        await Should.ThrowAsync<OptionsValidationException>(() => app.StartAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Startup_succeeds_without_a_running_redis_server()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Caching:Enabled"] = "true",
            ["ConnectionStrings:Redis"] = "127.0.0.1:1,connectRetry=0",
            ["urls"] = "http://127.0.0.1:0"
        });
        builder.AddResourceCache();
        await using var app = builder.Build();
        await app.StartAsync(TestContext.Current.CancellationToken);
        var cache = app.Services.GetRequiredService<ResourceCache>();
        (await cache.GetOrLoadAsync<string>(Guid.NewGuid(), "key",
            _ => Task.FromResult<string?>("database"), TestContext.Current.CancellationToken)).ShouldBe("database");
        await app.StopAsync(TestContext.Current.CancellationToken);
    }
}
