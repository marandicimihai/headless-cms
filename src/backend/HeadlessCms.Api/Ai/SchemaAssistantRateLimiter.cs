using System.Collections.Concurrent;

namespace HeadlessCms.Api.Ai;

public sealed class SchemaAssistantRateLimiter
{
    private const int PermitLimit = 5;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
    private readonly ConcurrentDictionary<string, WindowState> windows = new();

    public bool TryAcquire(string userId, out TimeSpan retryAfter)
    {
        var now = DateTimeOffset.UtcNow;
        if (windows.Count > 1024)
        {
            foreach (var item in windows)
            {
                lock (item.Value)
                {
                    if (now - item.Value.StartedAt >= Window)
                        windows.TryRemove(item.Key, out _);
                }
            }
        }

        var state = windows.GetOrAdd(userId, _ => new WindowState(now));
        lock (state)
        {
            if (now - state.StartedAt >= Window)
            {
                state.StartedAt = now;
                state.Count = 0;
            }

            if (state.Count >= PermitLimit)
            {
                retryAfter = Window - (now - state.StartedAt);
                return false;
            }

            state.Count++;
            retryAfter = TimeSpan.Zero;
            return true;
        }
    }

    private sealed class WindowState(DateTimeOffset startedAt)
    {
        public DateTimeOffset StartedAt { get; set; } = startedAt;
        public int Count { get; set; }
    }
}
