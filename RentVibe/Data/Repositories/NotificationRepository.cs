using Microsoft.EntityFrameworkCore;
using RentVibe.Models;

namespace RentVibe.Data.Repositories;

public class NotificationRepository
{
    private readonly AppDbContext _db;

    public NotificationRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Notification>> GetByUserAsync(string userId, int limit)
    {
        return await _db.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<int> GetUnreadCountAsync(string userId)
    {
        return await _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
    }

    public async Task<Notification?> GetByIdForUserAsync(int id, string userId)
    {
        return await _db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
    }

    public async Task MarkAllAsReadAsync(string userId)
    {
        await _db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(n => n.SetProperty(x => x.IsRead, true));
    }
}
