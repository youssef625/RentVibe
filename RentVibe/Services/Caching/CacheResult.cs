namespace RentVibe.Services.Caching;

public enum CacheSource
{
    L1,
    L2,
    Miss
}

public sealed record CacheResult<T>(T Value, CacheSource Source)
{
    public bool IsHit => Source != CacheSource.Miss;
}
