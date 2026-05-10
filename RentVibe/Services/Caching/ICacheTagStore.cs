namespace RentVibe.Services.Caching;

public interface ICacheTagStore
{
    Task AddAsync(IReadOnlyList<string> tags, string key);
    Task<IReadOnlyList<string>> GetKeysAsync(string tag);
    Task RemoveTagAsync(string tag);
}