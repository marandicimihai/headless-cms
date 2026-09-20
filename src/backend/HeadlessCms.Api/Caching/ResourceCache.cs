using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace HeadlessCms.Api.Caching;

public sealed class ResourceCacheOptions
{
    public bool Enabled { get; set; }
    public int ExpirationSeconds { get; set; } = 30;
    public string KeyPrefix { get; set; } = "";
}

public sealed class ResourceCache(
    IOptions<ResourceCacheOptions> options,
    IServiceProvider services,
    ILogger<ResourceCache> logger)
{
    private static readonly Meter Meter = new("HeadlessCms.ResourceCache");
    private static readonly Counter<long> Hits = Meter.CreateCounter<long>("cache.hits");
    private static readonly Counter<long> Misses = Meter.CreateCounter<long>("cache.misses");
    private static readonly Counter<long> Bypasses = Meter.CreateCounter<long>("cache.bypasses");
    private static readonly Counter<long> InvalidationFailures = Meter.CreateCounter<long>("cache.invalidation_failures");
    // Fixed stripes bound coordination memory even for arbitrary query strings.
    private readonly SemaphoreSlim[] gates = Enumerable.Range(0, 256).Select(_ => new SemaphoreSlim(1)).ToArray();
    private readonly ResourceCacheOptions settings = options.Value;
    private IDatabase Database => services.GetRequiredService<IConnectionMultiplexer>().GetDatabase();
    private string GenerationKey(Guid workspaceId) => $"{settings.KeyPrefix}:v1:{{{workspaceId:N}}}:generation";

    public static string RequestKey(HttpRequest request, string endpoint) => Convert.ToHexString(
        SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            endpoint,
            path = request.Path.Value,
            query = request.Query.OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => new { key = item.Key, values = item.Value.ToArray() })
        })));

    public async Task<T?> GetOrLoadAsync<T>(Guid workspaceId, string key,
        Func<CancellationToken, Task<T?>> load, CancellationToken ct = default) where T : class
    {
        if (!settings.Enabled)
        {
            Bypasses.Add(1);
            return await load(ct);
        }
        var gate = gates[(uint)StringComparer.Ordinal.GetHashCode(key) % (uint)gates.Length];
        await gate.WaitAsync(ct);
        try
        {
            IDatabase db;
            RedisValue generation;
            string responseKey;
            try
            {
                db = Database;
                var generationKey = GenerationKey(workspaceId);
                generation = (RedisValue)await db.ScriptEvaluateAsync(
                    "local g = redis.call('GET', KEYS[1]); if not g then g = ARGV[1]; redis.call('SET', KEYS[1], g); end; return g;",
                    [generationKey], [Guid.NewGuid().ToString("N")]);
                responseKey = $"{settings.KeyPrefix}:v1:{{{workspaceId:N}}}:{generation}:{key}";
                var cached = await db.StringGetAsync(responseKey);
                if (cached.HasValue)
                {
                    var result = JsonSerializer.Deserialize<T>((byte[])cached!);
                    if (result is not null)
                    {
                        Hits.Add(1);
                        return result;
                    }
                }
            }
            catch (Exception exception) when (exception is RedisException or JsonException)
            {
                Bypasses.Add(1);
                logger.LogWarning(exception, "Resource cache unavailable; reading PostgreSQL");
                return await load(ct);
            }
            Misses.Add(1);
            var value = await load(ct);
            ct.ThrowIfCancellationRequested();
            if (value is null) return null;
            var snapshot = JsonSerializer.SerializeToUtf8Bytes(value);
            try
            {
                await db.ScriptEvaluateAsync(
                    "if redis.call('GET', KEYS[1]) == ARGV[1] then return redis.call('SET', KEYS[2], ARGV[2], 'EX', ARGV[3]); end; return nil;",
                    [GenerationKey(workspaceId), responseKey], [generation, snapshot, settings.ExpirationSeconds]);
            }
            catch (RedisException exception)
            {
                Bypasses.Add(1);
                logger.LogWarning(exception, "Resource cache publication failed");
            }
            return JsonSerializer.Deserialize<T>(snapshot);
        }
        finally { gate.Release(); }
    }

    // Deliberately independent of request cancellation: the database write already committed.
    public async Task InvalidateWorkspaceAsync(Guid workspaceId)
    {
        if (!settings.Enabled) return;
        try
        {
            await Database.StringSetAsync(GenerationKey(workspaceId), Guid.NewGuid().ToString("N"));
        }
        catch (RedisException exception)
        {
            InvalidationFailures.Add(1);
            logger.LogWarning(exception, "Resource cache invalidation failed for workspace {WorkspaceId}", workspaceId);
        }
    }
}
