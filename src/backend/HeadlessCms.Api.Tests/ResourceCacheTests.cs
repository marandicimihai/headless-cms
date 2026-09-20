using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using HeadlessCms.Api.Caching;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using StackExchange.Redis;
using Xunit;

namespace HeadlessCms.Api.Tests;

public sealed class RedisFixture : IAsyncLifetime
{
    private readonly IContainer container = new ContainerBuilder("redis:7-alpine")
        .WithPortBinding(6379, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(6379))
        .Build();
    public IConnectionMultiplexer Connection { get; private set; } = null!;
    public async ValueTask InitializeAsync()
    {
        await container.StartAsync();
        Connection = await ConnectionMultiplexer.ConnectAsync($"{container.Hostname}:{container.GetMappedPublicPort(6379)}");
    }
    public Task StopAsync() => container.StopAsync();
    public async ValueTask DisposeAsync()
    {
        Connection.Dispose();
        await container.DisposeAsync();
    }
}

public sealed class ResourceCacheTests(RedisFixture redis) : IClassFixture<RedisFixture>
{
    public sealed record Snapshot(string Name, JsonElement Data);
    private static Snapshot Value(string name) => new(name, JsonSerializer.SerializeToElement(new { title = name }));
    private ResourceCache Create(string prefix, bool enabled = true, int expiry = 30, IConnectionMultiplexer? connection = null)
    {
        var provider = new ServiceCollection().AddSingleton(connection ?? redis.Connection).BuildServiceProvider();
        return new ResourceCache(Options.Create(new ResourceCacheOptions
        {
            Enabled = enabled,
            KeyPrefix = prefix,
            ExpirationSeconds = expiry
        }), provider, NullLogger<ResourceCache>.Instance);
    }

    [Fact]
    public async Task Shares_snapshots_across_instances_and_invalidates_all_workspace_keys()
    {
        var prefix = Guid.NewGuid().ToString("N");
        var first = Create(prefix); var second = Create(prefix);
        var workspace = Guid.NewGuid(); var other = Guid.NewGuid();
        var loads = 0;
        Task<Snapshot?> Load(CancellationToken _) { loads++; return Task.FromResult<Snapshot?>(Value("old")); }
        await first.GetOrLoadAsync(workspace, "individual", Load, TestContext.Current.CancellationToken);
        await first.GetOrLoadAsync(workspace, "list", Load, TestContext.Current.CancellationToken);
        await first.GetOrLoadAsync(other, "list", Load, TestContext.Current.CancellationToken);
        var cached = await second.GetOrLoadAsync(workspace, "individual", Load, TestContext.Current.CancellationToken);
        loads.ShouldBe(3);
        cached!.Data.GetProperty("title").GetString().ShouldBe("old");
        await second.InvalidateWorkspaceAsync(workspace);
        await first.GetOrLoadAsync(workspace, "individual", Load, TestContext.Current.CancellationToken);
        await first.GetOrLoadAsync(workspace, "list", Load, TestContext.Current.CancellationToken);
        await first.GetOrLoadAsync(other, "list", Load, TestContext.Current.CancellationToken);
        loads.ShouldBe(5);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Inflight_load_cannot_publish_after_invalidation_or_generation_eviction(bool evict)
    {
        var prefix = Guid.NewGuid().ToString("N"); var workspace = Guid.NewGuid();
        var cache = Create(prefix); var other = Create(prefix);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = cache.GetOrLoadAsync<Snapshot>(workspace, "key", async _ =>
        {
            started.SetResult(); await finish.Task; return Value("old");
        }, TestContext.Current.CancellationToken);
        await started.Task;
        if (evict) await redis.Connection.GetDatabase().KeyDeleteAsync($"{prefix}:v1:{{{workspace:N}}}:generation");
        else await other.InvalidateWorkspaceAsync(workspace);
        (await other.GetOrLoadAsync<Snapshot>(workspace, "key", _ => Task.FromResult<Snapshot?>(Value("new")), TestContext.Current.CancellationToken))!.Name.ShouldBe("new");
        finish.SetResult(); await pending;
        (await cache.GetOrLoadAsync<Snapshot>(workspace, "key", _ => throw new Exception("Unexpected load"), TestContext.Current.CancellationToken))!.Name.ShouldBe("new");
    }

    [Fact]
    public async Task Coalesces_misses_expires_and_does_not_cache_null_errors_or_cancellation()
    {
        var cache = Create(Guid.NewGuid().ToString("N"), expiry: 1); var workspace = Guid.NewGuid(); var loads = 0;
        async Task<Snapshot?> Load(CancellationToken _) { Interlocked.Increment(ref loads); await Task.Delay(20, TestContext.Current.CancellationToken); return Value("value"); }
        await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => cache.GetOrLoadAsync(workspace, "key", Load, TestContext.Current.CancellationToken)));
        loads.ShouldBe(1);
        await Task.Delay(1200, TestContext.Current.CancellationToken);
        await cache.GetOrLoadAsync(workspace, "key", Load, TestContext.Current.CancellationToken); loads.ShouldBe(2);
        await cache.GetOrLoadAsync<Snapshot>(workspace, "null", _ => Task.FromResult<Snapshot?>(null), TestContext.Current.CancellationToken);
        (await cache.GetOrLoadAsync(workspace, "null", Load, TestContext.Current.CancellationToken)).ShouldNotBeNull();
        await Should.ThrowAsync<InvalidOperationException>(() => cache.GetOrLoadAsync<Snapshot>(workspace, "error", _ => throw new InvalidOperationException(), TestContext.Current.CancellationToken));
        (await cache.GetOrLoadAsync(workspace, "error", Load, TestContext.Current.CancellationToken)).ShouldNotBeNull();
        using var cancellation = new CancellationTokenSource();
        await Should.ThrowAsync<OperationCanceledException>(() => cache.GetOrLoadAsync<Snapshot>(workspace, "cancel", _ =>
        {
            cancellation.Cancel(); return Task.FromResult<Snapshot?>(Value("cancelled"));
        }, cancellation.Token));
        (await cache.GetOrLoadAsync(workspace, "cancel", Load, TestContext.Current.CancellationToken))!.Name.ShouldBe("value");
    }

    [Fact]
    public async Task Disabled_or_unavailable_redis_bypasses_cache_and_invalidation_does_not_throw()
    {
        var config = ConfigurationOptions.Parse("127.0.0.1:1,abortConnect=false,connectTimeout=100,asyncTimeout=100,connectRetry=0");
        using var offline = await ConnectionMultiplexer.ConnectAsync(config);
        foreach (var enabled in new[] { false, true })
        {
            var cache = Create(Guid.NewGuid().ToString("N"), enabled, connection: offline); var loads = 0;
            Task<Snapshot?> Load(CancellationToken _) { loads++; return Task.FromResult<Snapshot?>(Value("value")); }
            var workspace = Guid.NewGuid();
            await cache.GetOrLoadAsync(workspace, "key", Load, TestContext.Current.CancellationToken);
            await cache.GetOrLoadAsync(workspace, "key", Load, TestContext.Current.CancellationToken);
            loads.ShouldBe(2);
            await cache.InvalidateWorkspaceAsync(workspace);
        }
    }

    [Fact]
    public async Task Connected_cache_falls_back_when_redis_stops()
    {
        var fixture = new RedisFixture();
        await fixture.InitializeAsync();
        try
        {
            var cache = Create(Guid.NewGuid().ToString("N"), connection: fixture.Connection);
            var workspace = Guid.NewGuid();
            await cache.GetOrLoadAsync<Snapshot>(workspace, "key",
                _ => Task.FromResult<Snapshot?>(Value("before")), TestContext.Current.CancellationToken);
            await fixture.StopAsync();
            (await cache.GetOrLoadAsync<Snapshot>(workspace, "key",
                _ => Task.FromResult<Snapshot?>(Value("after")), TestContext.Current.CancellationToken))!.Name.ShouldBe("after");
            await cache.InvalidateWorkspaceAsync(workspace);
        }
        finally { await fixture.DisposeAsync(); }
    }

    public sealed record MutableSnapshot(List<string> Names, JsonElement Data);

    [Fact]
    public async Task Responses_are_independent_of_source_documents_and_previous_responses()
    {
        var cache = Create(Guid.NewGuid().ToString("N"));
        var workspace = Guid.NewGuid();
        var document = JsonDocument.Parse("{\"title\":\"original\"}");
        var first = await cache.GetOrLoadAsync<MutableSnapshot>(workspace, "snapshot",
            _ => Task.FromResult<MutableSnapshot?>(new(["original"], document.RootElement)), TestContext.Current.CancellationToken);
        document.Dispose();
        first!.Names.Add("changed");
        first.Data.GetProperty("title").GetString().ShouldBe("original");
        var second = await cache.GetOrLoadAsync<MutableSnapshot>(workspace, "snapshot",
            _ => throw new InvalidOperationException("Expected cached snapshot"), TestContext.Current.CancellationToken);
        second!.Names.ShouldBe(["original"]);
        second.Data.GetProperty("title").GetString().ShouldBe("original");
    }

    [Fact]
    public void Keys_preserve_scope_and_all_query_values()
    {
        string Key(string path, string query)
        {
            var context = new DefaultHttpContext(); context.Request.Path = path; context.Request.QueryString = new QueryString(query);
            return ResourceCache.RequestKey(context.Request, "entries");
        }
        Key("/a", "?page=1&sort=-title").ShouldBe(Key("/a", "?sort=-title&page=1"));
        Key("/a", "?page=1").ShouldNotBe(Key("/b", "?page=1"));
        Key("/a", "?filter[x][eq]=a&filter[x][eq]=b").ShouldNotBe(Key("/a", "?filter[x][eq]=b&filter[x][eq]=a"));
        foreach (var query in new[] { "?page=2", "?pageSize=10", "?status=draft", "?sort=title", "?filter[x][eq]=a" })
            Key("/a", query).ShouldNotBe(Key("/a", "?page=1"));
    }
}
