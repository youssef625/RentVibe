using System.Security.Claims;
using HotChocolate;
using HotChocolate.Authorization;
using RentVibe.Data.Repositories;
using RentVibe.DTOs;
using RentVibe.Models;
using RentVibe.Models.Enums;
using RentVibe.Services.Caching;

namespace RentVibe.GraphQL.Mutations;

public class PropertyMutations
{
    [Authorize(Policy = "LandlordOnly")]
    public async Task<PropertyResponseDto> CreateProperty(
        CreatePropertyDto dto,
        [Service] DataRepository<Property> propertyRepo,
        [Service] IMultiLevelCache cache,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new GraphQLException("Unauthorized");
        }

        var property = new Property
        {
            LandlordId = userId,
            Title = dto.Title,
            Description = dto.Description,
            Price = dto.Price,
            Location = dto.Location,
            PropertyType = Enum.TryParse<PropertyType>(dto.PropertyType, true, out var pt) ? pt : PropertyType.Apartment,
            HasParking = dto.HasParking,
            HasElevator = dto.HasElevator,
            IsFurnished = dto.IsFurnished,
            Bedrooms = dto.Bedrooms,
            Bathrooms = dto.Bathrooms,
            AreaSqFt = dto.AreaSqFt,
            ApprovalStatus = ApprovalStatus.Pending
        };

        await propertyRepo.AddAsync(property);

        await cache.RemoveByTagAsync("properties:list");
        await cache.RemoveByTagAsync($"properties:detail:{property.Id}");

        return MapToDto(property);
    }

    [Authorize(Policy = "LandlordOnly")]
    public async Task<PropertyResponseDto?> UpdateProperty(
        int id,
        UpdatePropertyDto dto,
        [Service] PropertyRepository properties,
        [Service] DataRepository<Property> propertyRepo,
        [Service] IMultiLevelCache cache,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new GraphQLException("Unauthorized");
        }

        var property = await properties.GetByIdForLandlordAsync(id, userId);
        if (property is null) return null;

        property.Title = dto.Title;
        property.Description = dto.Description;
        property.Price = dto.Price;
        property.Location = dto.Location;
        property.PropertyType = Enum.TryParse<PropertyType>(dto.PropertyType, true, out var pt) ? pt : property.PropertyType;
        property.HasParking = dto.HasParking;
        property.HasElevator = dto.HasElevator;
        property.IsFurnished = dto.IsFurnished;
        property.Bedrooms = dto.Bedrooms;
        property.Bathrooms = dto.Bathrooms;
        property.AreaSqFt = dto.AreaSqFt;

        await propertyRepo.UpdateAsync(property);

        await cache.RemoveByTagAsync("properties:list");
        await cache.RemoveByTagAsync($"properties:detail:{property.Id}");

        return MapToDto(property);
    }

    [Authorize(Policy = "LandlordOnly")]
    public async Task<bool> DeleteProperty(
        int id,
        [Service] PropertyRepository properties,
        [Service] DataRepository<Property> propertyRepo,
        [Service] IMultiLevelCache cache,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new GraphQLException("Unauthorized");
        }

        var property = await properties.GetByIdForLandlordAsync(id, userId);
        if (property is null) return false;

        await propertyRepo.DeleteAsync(property);

        await cache.RemoveByTagAsync("properties:list");
        await cache.RemoveByTagAsync($"properties:detail:{property.Id}");

        return true;
    }

    private static PropertyResponseDto MapToDto(Property p) => new()
    {
        Id = p.Id,
        LandlordId = p.LandlordId,
        LandlordName = "",
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
        ImageUrls = new(),
        AverageRating = 0,
        ReviewCount = 0
    };
}