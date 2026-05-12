using Microsoft.EntityFrameworkCore;
using RentVibe.Models;
using RentVibe.Models.Enums;

namespace RentVibe.Data.Repositories;

public class VisitRepository
{
    private readonly AppDbContext _db;

    public VisitRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<bool> HasPendingVisitAsync(int propertyId, string tenantId)
    {
        return await _db.VisitAppointments
            .AnyAsync(v => v.PropertyId == propertyId
                           && v.TenantId == tenantId
                           && v.Status == VisitStatus.Pending);
    }

    public async Task<IReadOnlyList<VisitAppointment>> GetByTenantAsync(string tenantId)
    {
        return await _db.VisitAppointments
            .AsNoTracking()
            .Where(v => v.TenantId == tenantId)
            .Include(v => v.Property)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<VisitAppointment>> GetForLandlordAsync(string landlordId)
    {
        return await _db.VisitAppointments
            .AsNoTracking()
            .Where(v => v.Property.LandlordId == landlordId)
            .Include(v => v.Property)
            .Include(v => v.Tenant)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync();
    }

    public async Task<VisitAppointment?> GetByIdForLandlordAsync(int id, string landlordId)
    {
        return await _db.VisitAppointments
            .Include(v => v.Property)
            .FirstOrDefaultAsync(v => v.Id == id && v.Property.LandlordId == landlordId);
    }

    public async Task SaveChangesAsync()
    {
        await _db.SaveChangesAsync();
    }
}
