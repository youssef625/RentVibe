using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;

namespace RentVibe.Services.Caching;

public class MultiLevelCache : IMultiLevelCache
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly IMemoryCache _memoryCache;
    private readonly IDistributedCache _distributedCache;
    private readonly ICacheTagStore _tagStore;
    private readonly ILogger<MultiLevelCache> _logger;

    public MultiLevelCache(
        IMemoryCache memoryCache,
        IDistributedCache distributedCache,
        ICacheTagStore tagStore,
        ILogger<MultiLevelCache> logger)
    {
        _memoryCache = memoryCache;
        _distributedCache = distributedCache;
        _tagStore = tagStore;
        _logger = logger;
    }

    public async Task<T> GetOrCreateAsync<T>(string key, TimeSpan ttl, Func<Task<T>> factory, IReadOnlyList<string>? tags = null)
    {
        if (_memoryCache.TryGetValue(key, out T? cached) && cached is not null)
        {
            _logger.LogDebug("Cache hit (L1) {Key}", key);
            return cached;
        }

        try
        {
            var bytes = await _distributedCache.GetAsync(key);
            if (bytes is not null && bytes.Length > 0)
            {
                var value = JsonSerializer.Deserialize<T>(bytes, SerializerOptions);
                if (value is not null)
                {
                    _logger.LogDebug("Cache hit (L2) {Key}", key);
                    _memoryCache.Set(key, value, ttl);
                    return value;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read from distributed cache for key {Key}", key);
        }

        _logger.LogDebug("Cache miss {Key}", key);
        var created = await factory();
        
        try 
        {
            var entryOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl
            };

            var payload = JsonSerializer.SerializeToUtf8Bytes(created, SerializerOptions);
            await _distributedCache.SetAsync(key, payload, entryOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write to distributed cache for key {Key}", key);
        }
        
        _memoryCache.Set(key, created, ttl);

        if (tags is { Count: > 0 })
        {
            try
            {
                await _tagStore.AddAsync(tags, key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update cache tags for key {Key}", key);
            }
        }

        return created;
    }

    public async Task RemoveAsync(string key)
    {
        _memoryCache.Remove(key);
        await _distributedCache.RemoveAsync(key);
    }

    public async Task RemoveByTagAsync(string tag)
    {
        try
        {
            var keys = await _tagStore.GetKeysAsync(tag);
            foreach (var key in keys)
            {
                _memoryCache.Remove(key);
                await _distributedCache.RemoveAsync(key);
            }

            await _tagStore.RemoveTagAsync(tag);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove cache entries for tag {Tag}", tag);
        }
    }
}