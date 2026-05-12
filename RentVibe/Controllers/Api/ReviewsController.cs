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
public class ReviewsController : ControllerBase
{
    private readonly ReviewRepository _reviews;
    private readonly RentalApplicationRepository _applications;
    private readonly DataRepository<Review> _reviewRepo;
    private readonly DataRepository<Property> _propertyRepo;
    private readonly NotificationService _notifications;

    public ReviewsController(
        ReviewRepository reviews,
        RentalApplicationRepository applications,
        DataRepository<Review> reviewRepo,
        DataRepository<Property> propertyRepo,
        NotificationService notifications)
    {
        _reviews = reviews;
        _applications = applications;
        _reviewRepo = reviewRepo;
        _propertyRepo = propertyRepo;
        _notifications = notifications;
    }

    
    [HttpGet("property/{propertyId:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetByProperty(int propertyId)
    {
        var reviews = await _reviews.GetByPropertyAsync(propertyId);
        var result = reviews.Select(r => new
        {
            r.Id,
            r.Rating,
            r.Comment,
            TenantName = r.Tenant.FullName,
            r.CreatedAt
        });
        return Ok(result);
    }

    
    [HttpPost]
    [Authorize(Policy = "TenantOnly")]
    public async Task<IActionResult> Create([FromBody] CreateReviewDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        
        var hadRental = await _applications.TenantHadAcceptedRentalAsync(
            dto.PropertyId,
            userId,
            DateTime.UtcNow);

        if (!hadRental)
            return BadRequest(new { error = "You can only review a property you have rented." });

        var alreadyReviewed = await _reviews.ExistsAsync(dto.PropertyId, userId);

        if (alreadyReviewed)
            return BadRequest(new { error = "You have already reviewed this property." });

        var property = await _propertyRepo.GetByIdAsync(dto.PropertyId);
        if (property is null) return NotFound();

        var review = new Review
        {
            PropertyId = dto.PropertyId,
            TenantId = userId,
            Rating = dto.Rating,
            Comment = dto.Comment
        };

        await _reviewRepo.AddAsync(review);

        var tenantName = User.FindFirstValue(ClaimTypes.Name) ?? "A tenant";
        await _notifications.SendAsync(property.LandlordId,
            $"{tenantName} left a {dto.Rating}-star review on \"{property.Title}\".",
            NotificationType.NewReview, review.Id);

        return Ok(new { review.Id, message = "Review submitted." });
    }
}
