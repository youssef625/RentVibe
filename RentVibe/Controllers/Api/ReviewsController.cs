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
public class ReviewsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly NotificationService _notifications;

    public ReviewsController(AppDbContext db, NotificationService notifications)
    {
        _db = db;
        _notifications = notifications;
    }

    // Get reviews for a property (public)
    [HttpGet("property/{propertyId:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetByProperty(int propertyId)
    {
        var reviews = await _db.Reviews
            .Where(r => r.PropertyId == propertyId)
            .Include(r => r.Tenant)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new
            {
                r.Id,
                r.Rating,
                r.Comment,
                TenantName = r.Tenant.FullName,
                r.CreatedAt
            })
            .ToListAsync();
        return Ok(reviews);
    }

    // Tenant — submit a review (only after rental period, i.e. had an accepted application)
    [HttpPost]
    [Authorize(Policy = "TenantOnly")]
    public async Task<IActionResult> Create([FromBody] CreateReviewDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        // Verify tenant had an accepted application for this property
        var hadRental = await _db.RentalApplications
            .AnyAsync(a => a.PropertyId == dto.PropertyId
                        && a.TenantId == userId
                        && a.Status == ApplicationStatus.Accepted
                        && a.RentalEndDate <= DateTime.UtcNow);

        if (!hadRental)
            return BadRequest(new { error = "You can only review a property you have rented." });

        var alreadyReviewed = await _db.Reviews
            .AnyAsync(r => r.PropertyId == dto.PropertyId && r.TenantId == userId);

        if (alreadyReviewed)
            return BadRequest(new { error = "You have already reviewed this property." });

        var property = await _db.Properties.FindAsync(dto.PropertyId);
        if (property is null) return NotFound();

        var review = new Review
        {
            PropertyId = dto.PropertyId,
            TenantId = userId,
            Rating = dto.Rating,
            Comment = dto.Comment
        };

        _db.Reviews.Add(review);
        await _db.SaveChangesAsync();

        var tenantName = User.FindFirstValue(ClaimTypes.Name) ?? "A tenant";
        await _notifications.SendAsync(property.LandlordId,
            $"{tenantName} left a {dto.Rating}-star review on \"{property.Title}\".",
            NotificationType.NewReview, review.Id);

        return Ok(new { review.Id, message = "Review submitted." });
    }
}
