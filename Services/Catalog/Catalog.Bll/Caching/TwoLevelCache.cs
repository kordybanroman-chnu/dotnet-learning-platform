using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Catalog.Bll.Caching;

public interface ICatalogCache
{
    Task<T> GetOrCreateAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan l1Ttl,
        TimeSpan l2Ttl,
        CancellationToken ct = default) where T : class;

    Task InvalidateAsync(string key, string reason, CancellationToken ct = default);
}

public sealed class TwoLevelCache(
    IMemoryCache memory,
    IDistributedCache distributed,
    CacheMetrics metrics,
    ILogger<TwoLevelCache> logger) : ICatalogCache
{
    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan l1Ttl,
        TimeSpan l2Ttl,
        CancellationToken ct = default) where T : class
    {
        if (memory.TryGetValue(key, out T? cached) && cached is not null)
        {
            metrics.Hit("L1");
            logger.LogDebug("Cache hit {Key} at {CacheLevel}", key, "L1");
            return cached;
        }

        metrics.Miss("L1");
        logger.LogDebug("Cache miss {Key} at {CacheLevel}", key, "L1");

        try
        {
            var bytes = await distributed.GetAsync(key, ct);
            if (bytes is not null)
            {
                var value = JsonSerializer.Deserialize<T>(bytes);
                if (value is not null)
                {
                    metrics.Hit("L2");
                    logger.LogDebug("Cache hit {Key} at {CacheLevel}", key, "L2");
                    StoreInMemory(key, value, l1Ttl, l2Ttl);
                    return value;
                }
            }

            metrics.Miss("L2");
            logger.LogDebug("Cache miss {Key} at {CacheLevel}", key, "L2");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Redis read failed for {Key}", key);
        }

        var created = await factory();
        StoreInMemory(key, created, l1Ttl, l2Ttl);

        try
        {
            await distributed.SetAsync(
                key,
                JsonSerializer.SerializeToUtf8Bytes(created),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = l2Ttl },
                ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Redis write failed for {Key}", key);
        }

        return created;
    }

    public async Task InvalidateAsync(string key, string reason, CancellationToken ct = default)
    {
        memory.Remove(key);

        try
        {
            await distributed.RemoveAsync(key, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Redis invalidation failed for {Key}", key);
        }

        metrics.Invalidated(reason);
        logger.LogInformation("Cache invalidated {Key}, reason {Reason}", key, reason);
    }

    private void StoreInMemory<T>(string key, T value, TimeSpan l1Ttl, TimeSpan l2Ttl) where T : class
    {
        var absolute = TimeSpan.FromTicks(Math.Min(l2Ttl.Ticks, l1Ttl.Ticks * 2));
        memory.Set(key, value, new MemoryCacheEntryOptions
        {
            Size = 1,
            SlidingExpiration = l1Ttl,
            AbsoluteExpirationRelativeToNow = absolute,
            Priority = CacheItemPriority.Normal
        });
    }
}
