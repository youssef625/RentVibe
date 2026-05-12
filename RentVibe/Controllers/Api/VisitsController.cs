using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RentVibe.Data.Repositories;
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
    private readonly VisitRepository _visits;
    private readonly PropertyRepository _properties;
    private readonly DataRepository<VisitAppointment> _visitRepo;
    private readonly NotificationService _notifications;

    public VisitsController(
        VisitRepository visits,
        PropertyRepository properties,
        DataRepository<VisitAppointment> visitRepo,
        NotificationService notifications)
    {
        _visits = visits;
        _properties = properties;
        _visitRepo = visitRepo;
        _notifications = notifications;
    }

    
    [HttpPost]
    [Authorize(Policy = "TenantOnly")]
    public async Task<IActionResult> Create([FromBody] CreateVisitDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var property = await _properties.GetWithLandlordAsync(dto.PropertyId);
        if (property is null) return NotFound(new { error = "Property not found." });

        var hasPending = await _visits.HasPendingVisitAsync(dto.PropertyId, userId);
        if (hasPending) return BadRequest(new { error = "You already have a pending visit request for this property." });

        var visit = new VisitAppointment
        {
            PropertyId = dto.PropertyId,
            TenantId = userId,
            RequestedDate = dto.RequestedDate,
            Message = dto.Message
        };

        await _visitRepo.AddAsync(visit);

        
        var tenantName = User.FindFirstValue(ClaimTypes.Name) ?? "A tenant";
        await _notifications.SendAsync(property.LandlordId,
            $"{tenantName} requested a visit for \"{property.Title}\" on {dto.RequestedDate:MMM dd, yyyy}.",
            NotificationType.VisitRequested, visit.Id);

        return Ok(new { visit.Id, message = "Visit request submitted." });
    }

    
    [HttpGet("my")]
    [Authorize(Policy = "TenantOnly")]
    public async Task<IActionResult> GetMyVisits()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var visits = await _visits.GetByTenantAsync(userId);
        var result = visits.Select(v => new
        {
            v.Id,
            v.PropertyId,
            PropertyTitle = v.Property.Title,
            v.RequestedDate,
            Status = v.Status.ToString(),
            v.Message,
            v.CreatedAt
        });
        return Ok(result);
    }

    
    [HttpGet("landlord")]
    [Authorize(Policy = "LandlordOnly")]
    public async Task<IActionResult> GetLandlordVisits()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var visits = await _visits.GetForLandlordAsync(userId);
        var result = visits.Select(v => new
        {
            v.Id,
            v.PropertyId,
            PropertyTitle = v.Property.Title,
            TenantName = v.Tenant.FullName,
            TenantEmail = v.Tenant.Email,
            v.RequestedDate,
            Status = v.Status.ToString(),
            v.Message,
            v.CreatedAt
        });
        return Ok(result);
    }

    
    [HttpPost("{id:int}/accept")]
    [Authorize(Policy = "LandlordOnly")]
    public async Task<IActionResult> Accept(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var visit = await _visits.GetByIdForLandlordAsync(id, userId);

        if (visit is null) return NotFound();

        visit.Status = VisitStatus.Accepted;
        await _visits.SaveChangesAsync();

        await _notifications.SendAsync(visit.TenantId,
            $"Your visit request for \"{visit.Property.Title}\" on {visit.RequestedDate:MMM dd, yyyy} has been accepted!",
            NotificationType.VisitAccepted, visit.Id);

        return Ok(new { message = "Visit accepted." });
    }

    
    [HttpPost("{id:int}/reject")]
    [Authorize(Policy = "LandlordOnly")]
    public async Task<IActionResult> Reject(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var visit = await _visits.GetByIdForLandlordAsync(id, userId);

        if (visit is null) return NotFound();

        visit.Status = VisitStatus.Rejected;
        await _visits.SaveChangesAsync();

        await _notifications.SendAsync(visit.TenantId,
            $"Your visit request for \"{visit.Property.Title}\" has been rejected.",
            NotificationType.VisitRejected, visit.Id);

        return Ok(new { message = "Visit rejected." });
    }
}
