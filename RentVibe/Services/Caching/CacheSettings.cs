namespace RentVibe.Services.Caching;

public class CacheSettings
{
    public int DefaultTtlSeconds { get; set; } = 60;
    public int PropertyListTtlSeconds { get; set; } = 60;
    public int PropertyDetailTtlSeconds { get; set; } = 120;
    public int UserScopedTtlSeconds { get; set; } = 30;
}