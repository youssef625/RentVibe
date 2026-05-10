using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentVibe.Data;
using RentVibe.DTOs;
using RentVibe.Models;
using RentVibe.Models.Enums;
using RentVibe.Services;

namespace RentVibe.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class VisitsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly NotificationService _notifications;

    public VisitsController(AppDbContext db, NotificationService notifications)
    {
        _db = db;
        _notifications = notifications;
    }

    // Tenant — schedule a visit
    [HttpPost]
    [Authorize(Policy = "TenantOnly")]
    public async Task<IActionResult> Create([FromBody] CreateVisitDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var property = await _db.Properties.Include(p => p.Landlord).FirstOrDefaultAsync(p => p.Id == dto.PropertyId);
        if (property is null) return NotFound(new { error = "Property not found." });

        var hasPending = await _db.VisitAppointments.AnyAsync(v =>
            v.TenantId == userId && v.PropertyId == dto.PropertyId && v.Status == VisitStatus.Pending);
        if (hasPending) return BadRequest(new { error = "You already have a pending visit request for this property." });

        var visit = new VisitAppointment
        {
            PropertyId = dto.PropertyId,
            TenantId = userId,
            RequestedDate = dto.RequestedDate,
            Message = dto.Message
        };

        _db.VisitAppointments.Add(visit);
        await _db.SaveChangesAsync();

        // Notify landlord in real-time
        var tenantName = User.FindFirstValue(ClaimTypes.Name) ?? "A tenant";
        await _notifications.SendAsync(property.LandlordId,
            $"{tenantName} requested a visit for \"{property.Title}\" on {dto.RequestedDate:MMM dd, yyyy}.",
            NotificationType.VisitRequested, visit.Id);

        return Ok(new { visit.Id, message = "Visit request submitted." });
    }

    // Tenant — get my visit requests
    [HttpGet("my")]
    [Authorize(Policy = "TenantOnly")]
    public async Task<IActionResult> GetMyVisits()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var visits = await _db.VisitAppointments
            .Where(v => v.TenantId == userId)
            .Include(v => v.Property)
            .OrderByDescending(v => v.CreatedAt)
            .Select(v => new
            {
                v.Id, v.PropertyId,
                PropertyTitle = v.Property.Title,
                v.RequestedDate,
                Status = v.Status.ToString(),
                v.Message, v.CreatedAt
            })
            .ToListAsync();
        return Ok(visits);
    }

    // Landlord — get visit requests for my properties
    [HttpGet("landlord")]
    [Authorize(Policy = "LandlordOnly")]
    public async Task<IActionResult> GetLandlordVisits()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var visits = await _db.VisitAppointments
            .Where(v => v.Property.LandlordId == userId)
            .Include(v => v.Property)
            .Include(v => v.Tenant)
            .OrderByDescending(v => v.CreatedAt)
            .Select(v => new
            {
                v.Id, v.PropertyId,
                PropertyTitle = v.Property.Title,
                TenantName = v.Tenant.FullName,
                TenantEmail = v.Tenant.Email,
                v.RequestedDate,
                Status = v.Status.ToString(),
                v.Message, v.CreatedAt
            })
            .ToListAsync();
        return Ok(visits);
    }

    // Landlord — accept a visit
    [HttpPost("{id:int}/accept")]
    [Authorize(Policy = "LandlordOnly")]
    public async Task<IActionResult> Accept(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var visit = await _db.VisitAppointments
            .Include(v => v.Property)
            .FirstOrDefaultAsync(v => v.Id == id && v.Property.LandlordId == userId);

        if (visit is null) return NotFound();

        visit.Status = VisitStatus.Accepted;
        await _db.SaveChangesAsync();

        await _notifications.SendAsync(visit.TenantId,
            $"Your visit request for \"{visit.Property.Title}\" on {visit.RequestedDate:MMM dd, yyyy} has been accepted!",
            NotificationType.VisitAccepted, visit.Id);

        return Ok(new { message = "Visit accepted." });
    }

    // Landlord — reject a visit
    [HttpPost("{id:int}/reject")]
    [Authorize(Policy = "LandlordOnly")]
    public async Task<IActionResult> Reject(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var visit = await _db.VisitAppointments
            .Include(v => v.Property)
            .FirstOrDefaultAsync(v => v.Id == id && v.Property.LandlordId == userId);

        if (visit is null) return NotFound();

        visit.Status = VisitStatus.Rejected;
        await _db.SaveChangesAsync();

        await _notifications.SendAsync(visit.TenantId,
            $"Your visit request for \"{visit.Property.Title}\" has been rejected.",
            NotificationType.VisitRejected, visit.Id);

        return Ok(new { message = "Visit rejected." });
    }
}
