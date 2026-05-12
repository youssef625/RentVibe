using Microsoft.EntityFrameworkCore;
using RentVibe.Models;

namespace RentVibe.Data.Repositories;

public class FavoriteRepository
{
    private readonly AppDbContext _db;

    public FavoriteRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Favorite>> GetByTenantAsync(string tenantId)
    {
        return await _db.Favorites
            .AsNoTracking()
            .Where(f => f.TenantId == tenantId)
            .Include(f => f.Property)
                .ThenInclude(p => p.Images)
            .Include(f => f.Property)
                .ThenInclude(p => p.Landlord)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> ExistsAsync(string tenantId, int propertyId)
    {
        return await _db.Favorites.AnyAsync(f => f.TenantId == tenantId && f.PropertyId == propertyId);
    }

    public async Task<Favorite?> GetByTenantAndPropertyAsync(string tenantId, int propertyId)
    {
        return await _db.Favorites
            .FirstOrDefaultAsync(f => f.TenantId == tenantId && f.PropertyId == propertyId);
    }
}
