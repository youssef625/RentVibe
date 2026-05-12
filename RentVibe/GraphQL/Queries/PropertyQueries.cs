using Microsoft.Extensions.Options;
using RentVibe.Data.Repositories;
using RentVibe.DTOs;
using RentVibe.Models.Enums;
using RentVibe.Services.Caching;

namespace RentVibe.GraphQL.Queries;

public class PropertyQueries
{
    public async Task<IReadOnlyList<PropertyResponseDto>> GetProperties(
        string? search,
        string? location,
        decimal? minPrice,
        decimal? maxPrice,
        string? propertyType,
        [Service] PropertyRepository properties,
        [Service] IMultiLevelCache cache,
        [Service] CacheKeyBuilder keyBuilder,
        [Service] IOptions<CacheSettings> cacheOptions,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var args = new { search, location, minPrice, maxPrice, propertyType };
        var key = keyBuilder.Build("properties:list", args, null);
        var ttl = TimeSpan.FromSeconds(cacheOptions.Value.PropertyListTtlSeconds);

        var result = await cache.GetOrCreateWithMetadataAsync(key, ttl, async () =>
        {
            PropertyType? parsedType = null;
            if (!string.IsNullOrWhiteSpace(propertyType)
                && Enum.TryParse<PropertyType>(propertyType, true, out var pt))
            {
                parsedType = pt;
            }

            var entities = await properties.GetApprovedAvailableAsync(
                search,
                location,
                minPrice,
                maxPrice,
                parsedType);

            return entities.Select(p => MapToDto(p)).ToList();
        }, new[] { "properties:list" });

        SetCacheHeader(httpContextAccessor, result.Source);
        return result.Value;
    }

    public async Task<PropertyResponseDto?> GetPropertyById(
        int id,
        [Service] PropertyRepository properties,
        [Service] IMultiLevelCache cache,
        [Service] CacheKeyBuilder keyBuilder,
        [Service] IOptions<CacheSettings> cacheOptions,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var key = keyBuilder.Build("properties:detail", new { id }, null);
        var ttl = TimeSpan.FromSeconds(cacheOptions.Value.PropertyDetailTtlSeconds);
        var tag = $"properties:detail:{id}";

        var result = await cache.GetOrCreateWithMetadataAsync(key, ttl, async () =>
        {
            var property = await properties.GetByIdWithDetailsAsync(id);

            return property is null ? null : MapToDto(property);
        }, new[] { tag });

        SetCacheHeader(httpContextAccessor, result.Source);
        return result.Value;
    }

    private static void SetCacheHeader(IHttpContextAccessor httpContextAccessor, CacheSource source)
    {
        var response = httpContextAccessor.HttpContext?.Response;
        if (response is null)
        {
            return;
        }

        var headerValue = source switch
        {
            CacheSource.L1 => "HIT;L1",
            CacheSource.L2 => "HIT;L2",
            _ => "MISS"
        };

        response.Headers["X-Cache"] = headerValue;
    }

    private static PropertyResponseDto MapToDto(Models.Property p) => new()
    {
        Id = p.Id,
        LandlordId = p.LandlordId,
        LandlordName = p.Landlord?.FullName ?? "",
        Title = p.Title,
        Description = p.Description,
        Price = p.Price,
        Location = p.Location,
        PropertyType = p.PropertyType.ToString(),
        HasParking = p.HasParking,
        HasElevator = p.HasElevator,
        IsFurnished = p.IsFurnished,
        RentalStatus = p.RentalStatus.ToString(),
        ApprovalStatus = p.ApprovalStatus.ToString(),
        Bedrooms = p.Bedrooms,
        Bathrooms = p.Bathrooms,
        AreaSqFt = p.AreaSqFt,
        CreatedAt = p.CreatedAt,
        ImageUrls = p.Images?.Select(i => i.ImageUrl).ToList() ?? new(),
        AverageRating = p.Reviews?.Any() == true ? p.Reviews.Average(r => r.Rating) : 0,
        ReviewCount = p.Reviews?.Count ?? 0
    };
}