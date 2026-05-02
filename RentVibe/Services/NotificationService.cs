using Microsoft.AspNetCore.SignalR;
using RentVibe.Data;
using RentVibe.Hubs;
using RentVibe.Models;
using RentVibe.Models.Enums;

namespace RentVibe.Services;

public class NotificationService
{
    private readonly AppDbContext _db;
    private readonly IHubContext<NotificationHub> _hubContext;

    public NotificationService(AppDbContext db, IHubContext<NotificationHub> hubContext)
    {
        _db = db;
        _hubContext = hubContext;
    }

    public async Task SendAsync(string userId, string message, NotificationType type, int? referenceId = null)
    {
        var notification = new Notification
        {
            UserId = userId,
            Message = message,
            Type = type,
            ReferenceId = referenceId
        };

        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync();

        await _hubContext.Clients.Group(userId).SendAsync("ReceiveNotification", new
        {
            notification.Id,
            notification.Message,
            Type = type.ToString(),
            notification.ReferenceId,
            notification.CreatedAt,
            notification.IsRead
        });
    }
}
