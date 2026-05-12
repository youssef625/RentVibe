using Microsoft.AspNetCore.SignalR;
using RentVibe.Data.Repositories;
using RentVibe.Hubs;
using RentVibe.Models;
using RentVibe.Models.Enums;

namespace RentVibe.Services;

public class NotificationService
{
    private readonly DataRepository<Notification> _notificationRepo;
    private readonly IHubContext<NotificationHub> _hubContext;

    public NotificationService(DataRepository<Notification> notificationRepo, IHubContext<NotificationHub> hubContext)
    {
        _notificationRepo = notificationRepo;
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

        await _notificationRepo.AddAsync(notification);

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
