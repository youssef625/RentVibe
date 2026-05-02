using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentVibe.Data;
using RentVibe.Models;

namespace RentVibe.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "TenantOnly")]
public class FavoritesController : ControllerBase
{
    private readonly AppDbContext _db;

    public FavoritesController(AppDbContext db)
    {
        _db = db;
    }

    // Get my favorites
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var favorites = await _db.Favorites
            .Where(f => f.TenantId == userId)
            .Include(f => f.Property)
                .ThenInclude(p => p.Images)
            .Include(f => f.Property)
                .ThenInclude(p => p.Landlord)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new
            {
                
                f.PropertyId,
                Property = new
                {
                    f.Property.Id,
                    f.Property.Title,
                    f.Property.Location,
                    f.Property.Price,
                    LandlordName = f.Property.Landlord.FullName,
                    PropertyType = f.Property.PropertyType.ToString(),
                    ImageUrl = f.Property.Images.Select(i => i.ImageUrl).FirstOrDefault()
                },
                f.CreatedAt
            })
            .ToListAsync();
        return Ok(favorites);
    }

    // Add to favorites
    [HttpPost("{propertyId:int}")]
    public async Task<IActionResult> Add(int propertyId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var exists = await _db.Favorites.AnyAsync(f => f.TenantId == userId && f.PropertyId == propertyId);
        if (exists) return BadRequest(new { error = "Property already in favorites." });

        var propertyExists = await _db.Properties.AnyAsync(p => p.Id == propertyId);
        if (!propertyExists) return NotFound(new { error = "Property not found." });

        _db.Favorites.Add(new Favorite { TenantId = userId, PropertyId = propertyId });
        await _db.SaveChangesAsync();

        return Ok(new { message = "Added to favorites." });
    }

    // Remove from favorites
    [HttpDelete("{propertyId:int}")]
    public async Task<IActionResult> Remove(int propertyId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var fav = await _db.Favorites.FirstOrDefaultAsync(f => f.TenantId == userId && f.PropertyId == propertyId);
        if (fav is null) return NotFound();

        _db.Favorites.Remove(fav);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
