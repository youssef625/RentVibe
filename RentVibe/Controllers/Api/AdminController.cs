using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RentVibe.Data.Repositories;
using RentVibe.Models;
using RentVibe.Models.Enums;
using RentVibe.Services;

namespace RentVibe.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "AdminOnly")]
public class AdminController : ControllerBase
{
    private readonly ApprovalRepository _approvals;
    private readonly DataRepository<Property> _propertyRepo;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly NotificationService _notifications;

    public AdminController(
        ApprovalRepository approvals,
        DataRepository<Property> propertyRepo,
        UserManager<ApplicationUser> userManager,
        NotificationService notifications)
    {
        _approvals = approvals;
        _propertyRepo = propertyRepo;
        _userManager = userManager;
        _notifications = notifications;
    }

    

    [HttpGet("landlords/pending")]
    public async Task<IActionResult> GetPendingLandlords()
    {
        var landlords = await _approvals.GetPendingLandlordsAsync();
        var result = landlords.Select(u => new { u.Id, u.FullName, u.Email, u.CreatedAt });
        return Ok(result);
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

    

    [HttpGet("properties/pending")]
    public async Task<IActionResult> GetPendingProperties()
    {
        var properties = await _approvals.GetPendingPropertiesAsync();
        var result = properties.Select(p => new
        {
            p.Id,
            p.Title,
            p.Location,
            p.Price,
            PropertyType = p.PropertyType.ToString(),
            LandlordName = p.Landlord.FullName,
            p.CreatedAt,
            ImageUrls = p.Images.Select(i => i.ImageUrl).ToList()
        });
        return Ok(result);
    }

    [HttpPost("properties/{id}/approve")]
    public async Task<IActionResult> ApproveProperty(int id)
    {
        var property = await _propertyRepo.GetByIdAsync(id);
        if (property is null) return NotFound();

        property.ApprovalStatus = ApprovalStatus.Approved;
        await _propertyRepo.UpdateAsync(property);

        await _notifications.SendAsync(property.LandlordId,
            $"Your property \"{property.Title}\" has been approved and is now visible.",
            NotificationType.PropertyApproved, property.Id);

        return Ok(new { message = "Property approved." });
    }

    [HttpPost("properties/{id}/reject")]
    public async Task<IActionResult> RejectProperty(int id)
    {
        var property = await _propertyRepo.GetByIdAsync(id);
        if (property is null) return NotFound();

        property.ApprovalStatus = ApprovalStatus.Rejected;
        await _propertyRepo.UpdateAsync(property);

        await _notifications.SendAsync(property.LandlordId,
            $"Your property \"{property.Title}\" has been rejected.",
            NotificationType.PropertyRejected, property.Id);

        return Ok(new { message = "Property rejected." });
    }
}
