using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RentVibe.Data.Repositories;
using RentVibe.Models;

namespace RentVibe.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "TenantOnly")]
public class FavoritesController : ControllerBase
{
    private readonly FavoriteRepository _favorites;
    private readonly PropertyRepository _properties;
    private readonly DataRepository<Favorite> _favoriteRepo;

    public FavoritesController(
        FavoriteRepository favorites,
        PropertyRepository properties,
        DataRepository<Favorite> favoriteRepo)
    {
        _favorites = favorites;
        _properties = properties;
        _favoriteRepo = favoriteRepo;
    }

    
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var favorites = await _favorites.GetByTenantAsync(userId);
        var result = favorites.Select(f => new
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
        });
        return Ok(result);
    }

    
    [HttpPost("{propertyId:int}")]
    public async Task<IActionResult> Add(int propertyId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var exists = await _favorites.ExistsAsync(userId, propertyId);
        if (exists) return BadRequest(new { error = "Property already in favorites." });

        var propertyExists = await _properties.ExistsAsync(propertyId);
        if (!propertyExists) return NotFound(new { error = "Property not found." });

        await _favoriteRepo.AddAsync(new Favorite { TenantId = userId, PropertyId = propertyId });

        return Ok(new { message = "Added to favorites." });
    }

    
    [HttpDelete("{propertyId:int}")]
    public async Task<IActionResult> Remove(int propertyId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var fav = await _favorites.GetByTenantAndPropertyAsync(userId, propertyId);
        if (fav is null) return NotFound();

        await _favoriteRepo.DeleteAsync(fav);
        return NoContent();
    }
}
