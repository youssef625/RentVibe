using Microsoft.EntityFrameworkCore;
using RentVibe.Models;
using RentVibe.Models.Enums;

namespace RentVibe.Data.Repositories;

public class ApprovalRepository
{
    private readonly AppDbContext _db;

    public ApprovalRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ApplicationUser>> GetPendingLandlordsAsync()
    {
        return await _db.Users
            .AsNoTracking()
            .Where(u => u.Role == UserRole.Landlord && u.AccountStatus == AccountStatus.Pending)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Property>> GetPendingPropertiesAsync()
    {
        return await _db.Properties
            .AsNoTracking()
            .Where(p => p.ApprovalStatus == ApprovalStatus.Pending)
            .Include(p => p.Landlord)
            .Include(p => p.Images)
            .ToListAsync();
    }
}
