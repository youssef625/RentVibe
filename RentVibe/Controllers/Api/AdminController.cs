using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentVibe.Data;
using RentVibe.Models;
using RentVibe.Models.Enums;
using RentVibe.Services;

namespace RentVibe.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "AdminOnly")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly NotificationService _notifications;

    public AdminController(AppDbContext db, UserManager<ApplicationUser> userManager, NotificationService notifications)
    {
        _db = db;
        _userManager = userManager;
        _notifications = notifications;
    }

    // --- Landlord account management ---

    [HttpGet("landlords/pending")]
    public async Task<IActionResult> GetPendingLandlords()
    {
        var landlords = await _db.Users
            .Where(u => u.Role == UserRole.Landlord && u.AccountStatus == AccountStatus.Pending)
            .Select(u => new { u.Id, u.FullName, u.Email, u.CreatedAt })
            .ToListAsync();
        return Ok(landlords);
    }

    [HttpPost("landlords/{id}/approve")]
    public async Task<IActionResult> ApproveLandlord(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null || user.Role != UserRole.Landlord)
            return NotFound();

        user.AccountStatus = AccountStatus.Approved;
        await _userManager.UpdateAsync(user);

        await _notifications.SendAsync(user.Id,
            "Your landlord account has been approved! You can now list properties.",
            NotificationType.AccountApproved);

        return Ok(new { message = "Landlord approved." });
    }

    [HttpPost("landlords/{id}/reject")]
    public async Task<IActionResult> RejectLandlord(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null || user.Role != UserRole.Landlord)
            return NotFound();

        user.AccountStatus = AccountStatus.Rejected;
        await _userManager.UpdateAsync(user);

        await _notifications.SendAsync(user.Id,
            "Your landlord account has been rejected.",
            NotificationType.AccountRejected);

        return Ok(new { message = "Landlord rejected." });
    }

    // --- Property approval management ---

    [HttpGet("properties/pending")]
    public async Task<IActionResult> GetPendingProperties()
    {
        var properties = await _db.Properties
            .Where(p => p.ApprovalStatus == ApprovalStatus.Pending)
            .Include(p => p.Landlord)
            .Include(p => p.Images)
            .Select(p => new
            {
                p.Id, p.Title, p.Location, p.Price,
                PropertyType = p.PropertyType.ToString(),
                LandlordName = p.Landlord.FullName,
                p.CreatedAt,
                ImageUrls = p.Images.Select(i => i.ImageUrl).ToList()
            })
            .ToListAsync();
        return Ok(properties);
    }

    [HttpPost("properties/{id}/approve")]
    public async Task<IActionResult> ApproveProperty(int id)
    {
        var property = await _db.Properties.FindAsync(id);
        if (property is null) return NotFound();

        property.ApprovalStatus = ApprovalStatus.Approved;
        await _db.SaveChangesAsync();

        await _notifications.SendAsync(property.LandlordId,
            $"Your property \"{property.Title}\" has been approved and is now visible.",
            NotificationType.PropertyApproved, property.Id);

        return Ok(new { message = "Property approved." });
    }

    [HttpPost("properties/{id}/reject")]
    public async Task<IActionResult> RejectProperty(int id)
    {
        var property = await _db.Properties.FindAsync(id);
        if (property is null) return NotFound();

        property.ApprovalStatus = ApprovalStatus.Rejected;
        await _db.SaveChangesAsync();

        await _notifications.SendAsync(property.LandlordId,
            $"Your property \"{property.Title}\" has been rejected.",
            NotificationType.PropertyRejected, property.Id);

        return Ok(new { message = "Property rejected." });
    }
}
