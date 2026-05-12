using Microsoft.EntityFrameworkCore;
using RentVibe.Models;

namespace RentVibe.Data.Repositories;

public class ReviewRepository
{
    private readonly AppDbContext _db;

    public ReviewRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Review>> GetByPropertyAsync(int propertyId)
    {
        return await _db.Reviews
            .AsNoTracking()
            .Where(r => r.PropertyId == propertyId)
            .Include(r => r.Tenant)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> ExistsAsync(int propertyId, string tenantId)
    {
        return await _db.Reviews.AnyAsync(r => r.PropertyId == propertyId && r.TenantId == tenantId);
    }
}
