using Microsoft.EntityFrameworkCore;
using RentVibe.Models;
using RentVibe.Models.Enums;

namespace RentVibe.Data.Repositories;

public class PropertyRepository
{
    private readonly AppDbContext _db;

    public PropertyRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Property>> GetApprovedAvailableAsync(
        string? search,
        string? location,
        decimal? minPrice,
        decimal? maxPrice,
        PropertyType? propertyType)
    {
        var query = _db.Properties
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

        if (propertyType.HasValue)
            query = query.Where(p => p.PropertyType == propertyType);

        return await query
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<Property?> GetByIdWithDetailsAsync(int id)
    {
        return await _db.Properties
            .AsNoTracking()
            .Include(p => p.Landlord)
            .Include(p => p.Images)
            .Include(p => p.Reviews)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<IReadOnlyList<Property>> GetByLandlordAsync(string landlordId)
    {
        return await _db.Properties
            .AsNoTracking()
            .Where(p => p.LandlordId == landlordId)
            .Include(p => p.Landlord)
            .Include(p => p.Images)
            .Include(p => p.Reviews)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<Property?> GetByIdForLandlordAsync(int id, string landlordId)
    {
        return await _db.Properties
            .FirstOrDefaultAsync(p => p.Id == id && p.LandlordId == landlordId);
    }

    public async Task<Property?> GetWithLandlordAsync(int id)
    {
        return await _db.Properties
            .Include(p => p.Landlord)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<bool> ExistsAsync(int id)
    {
        return await _db.Properties.AnyAsync(p => p.Id == id);
    }

    public async Task<PropertyImage?> GetImageForLandlordAsync(int propertyId, string imageUrl, string landlordId)
    {
        return await _db.PropertyImages
            .Include(i => i.Property)
            .FirstOrDefaultAsync(i => i.PropertyId == propertyId
                                      && i.ImageUrl == imageUrl
                                      && i.Property.LandlordId == landlordId);
    }

    public async Task AddImagesAsync(IEnumerable<PropertyImage> images)
    {
        _db.PropertyImages.AddRange(images);
        await _db.SaveChangesAsync();
    }
}
