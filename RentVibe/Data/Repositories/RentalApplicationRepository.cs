using Microsoft.EntityFrameworkCore;
using RentVibe.Models;
using RentVibe.Models.Enums;

namespace RentVibe.Data.Repositories;

public class RentalApplicationRepository
{
    private readonly AppDbContext _db;

    public RentalApplicationRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<bool> HasPendingApplicationAsync(int propertyId, string tenantId)
    {
        return await _db.RentalApplications
            .AnyAsync(a => a.PropertyId == propertyId
                           && a.TenantId == tenantId
                           && a.Status == ApplicationStatus.Pending);
    }

    public async Task<RentalApplication?> GetByIdForTenantAsync(int id, string tenantId)
    {
        return await _db.RentalApplications
            .FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId);
    }

    public async Task<RentalApplication?> GetByIdForLandlordAsync(int id, string landlordId)
    {
        return await _db.RentalApplications
            .Include(a => a.Property)
            .FirstOrDefaultAsync(a => a.Id == id && a.Property.LandlordId == landlordId);
    }

    public async Task<IReadOnlyList<RentalApplication>> GetByTenantAsync(string tenantId)
    {
        return await _db.RentalApplications
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId)
            .Include(a => a.Property)
            .Include(a => a.Documents)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<RentalApplication>> GetForLandlordAsync(string landlordId)
    {
        return await _db.RentalApplications
            .AsNoTracking()
            .Where(a => a.Property.LandlordId == landlordId)
            .Include(a => a.Property)
            .Include(a => a.Tenant)
            .Include(a => a.Documents)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<RentalApplication>> GetOtherPendingForPropertyAsync(int propertyId, int excludeId)
    {
        return await _db.RentalApplications
            .Where(a => a.PropertyId == propertyId
                        && a.Id != excludeId
                        && a.Status == ApplicationStatus.Pending)
            .ToListAsync();
    }

    public async Task<ApplicationDocument?> GetDocumentWithApplicationAsync(int documentId, int applicationId)
    {
        return await _db.ApplicationDocuments
            .Include(d => d.RentalApplication)
                .ThenInclude(a => a.Property)
            .FirstOrDefaultAsync(d => d.Id == documentId && d.RentalApplicationId == applicationId);
    }

    public async Task<bool> TenantHadAcceptedRentalAsync(int propertyId, string tenantId, DateTime now)
    {
        return await _db.RentalApplications
            .AnyAsync(a => a.PropertyId == propertyId
                           && a.TenantId == tenantId
                           && a.Status == ApplicationStatus.Accepted
                           && a.RentalEndDate <= now);
    }

    public async Task AddDocumentsAsync(IEnumerable<ApplicationDocument> documents)
    {
        _db.ApplicationDocuments.AddRange(documents);
        await _db.SaveChangesAsync();
    }

    public async Task SaveChangesAsync()
    {
        await _db.SaveChangesAsync();
    }
}
