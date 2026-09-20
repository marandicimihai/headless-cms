using StackExchange.Redis;

namespace HeadlessCms.Api.Caching;

public static class CacheConfiguration
{
    public static void AddResourceCache(this WebApplicationBuilder builder)
    {
        var section = builder.Configuration.GetSection("Caching");
        var settings = section.Get<ResourceCacheOptions>() ?? new();
        if (string.IsNullOrWhiteSpace(settings.KeyPrefix))
            settings.KeyPrefix = $"headless-cms:{builder.Environment.EnvironmentName.ToLowerInvariant()}";
        builder.Services.AddOptions<ResourceCacheOptions>()
            .Configure(o =>
            {
                o.Enabled = settings.Enabled;
                o.ExpirationSeconds = settings.ExpirationSeconds;
                o.KeyPrefix = settings.KeyPrefix;
            })
            .Validate(o => o.ExpirationSeconds > 0, "Caching:ExpirationSeconds must be positive")
            .Validate(o => !o.KeyPrefix.Contains('{') && !o.KeyPrefix.Contains('}'), "Caching:KeyPrefix cannot contain Redis hash tags")
            .ValidateOnStart();
        if (settings.Enabled)
        {
            var connection = builder.Configuration.GetConnectionString("Redis");
            if (string.IsNullOrWhiteSpace(connection))
                throw new InvalidOperationException("ConnectionStrings:Redis is required when caching is enabled");
            var redisOptions = ConfigurationOptions.Parse(connection);
            if (redisOptions.EndPoints.Count == 0)
                throw new InvalidOperationException("Redis requires at least one endpoint");
            redisOptions.AbortOnConnectFail = false;
            redisOptions.ConnectTimeout = 1000;
            redisOptions.AsyncTimeout = 1000;
            builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisOptions));
        }
        builder.Services.AddSingleton<ResourceCache>();
    }
}
