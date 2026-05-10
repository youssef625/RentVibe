using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RentVibe.Data;
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
        [Service] AppDbContext db,
        [Service] IMultiLevelCache cache,
        [Service] CacheKeyBuilder keyBuilder,
        [Service] IOptions<CacheSettings> cacheOptions)
    {
        var args = new { search, location, minPrice, maxPrice, propertyType };
        var key = keyBuilder.Build("properties:list", args, null);
        var ttl = TimeSpan.FromSeconds(cacheOptions.Value.PropertyListTtlSeconds);

        return await cache.GetOrCreateAsync(key, ttl, async () =>
        {
            var query = db.Properties
                .AsNoTracking()
                .Where(p => p.ApprovalStatus == ApprovalStatus.Approved
                            && p.RentalStatus == RentalStatus.Available)
                .Include(p => p.Landlord)
                .Include(p => p.Images)
                .Include(p => p.Reviews)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(p => p.Title.Contains(search) || p.Description!.Contains(search));

            if (!string.IsNullOrWhiteSpace(location))
                query = query.Where(p => p.Location.Contains(location));

            if (minPrice.HasValue)
                query = query.Where(p => p.Price >= minPrice.Value);

            if (maxPrice.HasValue)
                query = query.Where(p => p.Price <= maxPrice.Value);

            if (!string.IsNullOrWhiteSpace(propertyType)
                && Enum.TryParse<PropertyType>(propertyType, true, out var pt))
            {
                query = query.Where(p => p.PropertyType == pt);
            }

            var entities = await query
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return entities.Select(p => MapToDto(p)).ToList();
        }, new[] { "properties:list" });
    }

    public async Task<PropertyResponseDto?> GetPropertyById(
        int id,
        [Service] AppDbContext db,
        [Service] IMultiLevelCache cache,
        [Service] CacheKeyBuilder keyBuilder,
        [Service] IOptions<CacheSettings> cacheOptions)
    {
        var key = keyBuilder.Build("properties:detail", new { id }, null);
        var ttl = TimeSpan.FromSeconds(cacheOptions.Value.PropertyDetailTtlSeconds);
        var tag = $"properties:detail:{id}";

        return await cache.GetOrCreateAsync(key, ttl, async () =>
        {
            var property = await db.Properties
                .AsNoTracking()
                .Include(p => p.Landlord)
                .Include(p => p.Images)
                .Include(p => p.Reviews)
                .FirstOrDefaultAsync(p => p.Id == id);

            return property is null ? null : MapToDto(property);
        }, new[] { tag });
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