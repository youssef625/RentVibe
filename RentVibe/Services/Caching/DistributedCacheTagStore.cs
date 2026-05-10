using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace RentVibe.Services.Caching;

public class DistributedCacheTagStore : ICacheTagStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly IDistributedCache _distributedCache;

    public DistributedCacheTagStore(IDistributedCache distributedCache)
    {
        _distributedCache = distributedCache;
    }

    public async Task AddAsync(IReadOnlyList<string> tags, string key)
    {
        foreach (var tag in tags)
        {
            var storeKey = BuildTagKey(tag);
            var keys = await GetKeysInternalAsync(storeKey);
            if (keys.Add(key))
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(keys, SerializerOptions);
                await _distributedCache.SetAsync(storeKey, bytes, new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6)
                });
            }
        }
    }

    public async Task<IReadOnlyList<string>> GetKeysAsync(string tag)
    {
        var storeKey = BuildTagKey(tag);
        var keys = await GetKeysInternalAsync(storeKey);
        return keys.ToList();
    }

    public Task RemoveTagAsync(string tag)
    {
        var storeKey = BuildTagKey(tag);
        return _distributedCache.RemoveAsync(storeKey);
    }

    private async Task<HashSet<string>> GetKeysInternalAsync(string storeKey)
    {
        var bytes = await _distributedCache.GetAsync(storeKey);
        if (bytes is null || bytes.Length == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var keys = JsonSerializer.Deserialize<HashSet<string>>(bytes, SerializerOptions);
        return keys ?? new HashSet<string>(StringComparer.Ordinal);
    }

    private static string BuildTagKey(string tag) => $"cachetag:{tag}";
}