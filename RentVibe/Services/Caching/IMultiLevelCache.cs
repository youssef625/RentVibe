namespace RentVibe.Services.Caching;

public interface IMultiLevelCache
{
    Task<T> GetOrCreateAsync<T>(string key, TimeSpan ttl, Func<Task<T>> factory, IReadOnlyList<string>? tags = null);
    Task RemoveAsync(string key);
    Task RemoveByTagAsync(string tag);
}