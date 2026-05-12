using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RentVibe.Data.Repositories;

namespace RentVibe.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly NotificationRepository _notifications;
    private readonly DataRepository<Models.Notification> _notificationRepo;

    public NotificationsController(
        NotificationRepository notifications,
        DataRepository<Models.Notification> notificationRepo)
    {
        _notifications = notifications;
        _notificationRepo = notificationRepo;
    }

    
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int limit = 50)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        limit = Math.Clamp(limit, 1, 50);

        var notifications = await _notifications.GetByUserAsync(userId, limit);
        var result = notifications.Select(n => new
        {
            n.Id,
            n.Message,
            Type = n.Type.ToString(),
            n.ReferenceId,
            n.IsRead,
            n.CreatedAt
        });
        return Ok(result);
    }

    
    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var count = await _notifications.GetUnreadCountAsync(userId);
        return Ok(new { count });
    }

    
    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var notification = await _notifications.GetByIdForUserAsync(id, userId);
        if (notification is null) return NotFound();

        notification.IsRead = true;
        await _notificationRepo.UpdateAsync(notification);
        return Ok(new { message = "Marked as read." });
    }

    
    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _notifications.MarkAllAsReadAsync(userId);
        return Ok(new { message = "All notifications marked as read." });
    }
}
