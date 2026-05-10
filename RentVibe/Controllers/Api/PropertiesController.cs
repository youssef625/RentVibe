using System.Security.Claims;
using Path = System.IO.Path;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentVibe.Data;
using RentVibe.DTOs;
using RentVibe.Models;
using RentVibe.Models.Enums;

namespace RentVibe.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public class PropertiesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public PropertiesController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    // Public — browse approved properties with search/filter
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? location,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] string? propertyType)
    {
        var query = _db.Properties
            .Where(p => p.ApprovalStatus == ApprovalStatus.Approved
                        && p.RentalStatus == RentalStatus.Available)
            .Include(p => p.Landlord)
            .Include(p => p.Images)
            .Include(p => p.Reviews)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.Title.Contains(search) || p.Description!.Contains(search));

        if (!string.IsNullOrWhiteSpace(location))
            query = query.Where(p => p.Location.Contains(location));

        if (minPrice.HasValue)
            query = query.Where(p => p.Price >= minPrice.Value);

        if (maxPrice.HasValue)
            query = query.Where(p => p.Price <= maxPrice.Value);

        if (!string.IsNullOrWhiteSpace(propertyType) && Enum.TryParse<PropertyType>(propertyType, true, out var pt))
            query = query.Where(p => p.PropertyType == pt);

        var properties = await query
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => MapToDto(p))
            .ToListAsync();

        return Ok(properties);
    }

    // Public — get single property
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var property = await _db.Properties
            .Include(p => p.Landlord)
            .Include(p => p.Images)
            .Include(p => p.Reviews)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (property is null) return NotFound();
        return Ok(MapToDto(property));
    }

    // Landlord — get own properties (all statuses)
    [HttpGet("my")]
    [Authorize(Policy = "LandlordOnly")]
    public async Task<IActionResult> GetMyProperties()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var properties = await _db.Properties
            .Where(p => p.LandlordId == userId)
            .Include(p => p.Landlord)
            .Include(p => p.Images)
            .Include(p => p.Reviews)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => MapToDto(p))
            .ToListAsync();

        return Ok(properties);
    }

    // Landlord — create property
    [HttpPost]
    [Authorize(Policy = "LandlordOnly")]
    public async Task<IActionResult> Create([FromBody] CreatePropertyDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var property = new Property
        {
            LandlordId = userId,
            Title = dto.Title,
            Description = dto.Description,
            Price = dto.Price,
            Location = dto.Location,
            PropertyType = Enum.TryParse<PropertyType>(dto.PropertyType, true, out var pt) ? pt : PropertyType.Apartment,
            HasParking = dto.HasParking,
            HasElevator = dto.HasElevator,
            IsFurnished = dto.IsFurnished,
            Bedrooms = dto.Bedrooms,
            Bathrooms = dto.Bathrooms,
            AreaSqFt = dto.AreaSqFt,
            ApprovalStatus = ApprovalStatus.Pending
        };

        _db.Properties.Add(property);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = property.Id }, new { property.Id });
    }

    // Landlord — update own property
    [HttpPut("{id:int}")]
    [Authorize(Policy = "LandlordOnly")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdatePropertyDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var property = await _db.Properties.FirstOrDefaultAsync(p => p.Id == id && p.LandlordId == userId);
        if (property is null) return NotFound();

        property.Title = dto.Title;
        property.Description = dto.Description;
        property.Price = dto.Price;
        property.Location = dto.Location;
        property.PropertyType = Enum.TryParse<PropertyType>(dto.PropertyType, true, out var pt) ? pt : property.PropertyType;
        property.HasParking = dto.HasParking;
        property.HasElevator = dto.HasElevator;
        property.IsFurnished = dto.IsFurnished;
        property.Bedrooms = dto.Bedrooms;
        property.Bathrooms = dto.Bathrooms;
        property.AreaSqFt = dto.AreaSqFt;

        await _db.SaveChangesAsync();
        return Ok(new { message = "Property updated." });
    }

    // Landlord — delete own property
    [HttpDelete("{id:int}")]
    [Authorize(Policy = "LandlordOnly")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var property = await _db.Properties.FirstOrDefaultAsync(p => p.Id == id && p.LandlordId == userId);
        if (property is null) return NotFound();

        _db.Properties.Remove(property);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Landlord — upload images for a property
    [HttpPost("{id:int}/images")]
    [Authorize(Policy = "LandlordOnly")]
    public async Task<IActionResult> UploadImages(int id, [FromForm] List<IFormFile> files)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var property = await _db.Properties.FirstOrDefaultAsync(p => p.Id == id && p.LandlordId == userId);
        if (property is null) return NotFound();

        var uploadDir = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads", "properties");
        Directory.CreateDirectory(uploadDir);

        var urls = new List<string>();
        foreach (var file in files)
        {
            if (file.Length == 0) continue;
            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadDir, fileName);

            await using var stream = new System.IO.FileStream(filePath, System.IO.FileMode.Create);
            await file.CopyToAsync(stream);

            var url = $"/uploads/properties/{fileName}";
            _db.PropertyImages.Add(new PropertyImage { PropertyId = id, ImageUrl = url });
            urls.Add(url);
        }

        await _db.SaveChangesAsync();
        return Ok(new { imageUrls = urls });
    }

    [HttpDelete("{propertyId:int}/images")]
    [Authorize(Policy = "LandlordOnly")]
    public async Task<IActionResult> DeleteImage(int propertyId, [FromQuery] string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return BadRequest(new { message = "imageUrl is required." });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var image = await _db.PropertyImages
            .Include(i => i.Property)
            .FirstOrDefaultAsync(i => i.PropertyId == propertyId && i.ImageUrl == imageUrl && i.Property.LandlordId == userId);

        if (image is null) return NotFound();

        var filePath = Path.Combine(_env.WebRootPath ?? "wwwroot", image.ImageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (System.IO.File.Exists(filePath))
            System.IO.File.Delete(filePath);

        _db.PropertyImages.Remove(image);
        await _db.SaveChangesAsync();
        return NoContent();
    }



    private static PropertyResponseDto MapToDto(Property p) => new()
    {
        Id = p.Id,
        LandlordId = p.LandlordId,
        LandlordName = p.Landlord?.FullName ?? "",
        Title = p.Title,
        Description = p.Description,
        Price = p.Price,
        Location = p.Location,
        PropertyType = p.PropertyType.ToString(),
        HasParking = p.HasParking,
        HasElevator = p.HasElevator,
        IsFurnished = p.IsFurnished,
        RentalStatus = p.RentalStatus.ToString(),
        ApprovalStatus = p.ApprovalStatus.ToString(),
        Bedrooms = p.Bedrooms,
        Bathrooms = p.Bathrooms,
        AreaSqFt = p.AreaSqFt,
        CreatedAt = p.CreatedAt,
        ImageUrls = p.Images?.Select(i => i.ImageUrl).ToList() ?? new(),
        AverageRating = p.Reviews?.Any() == true ? p.Reviews.Average(r => r.Rating) : 0,
        ReviewCount = p.Reviews?.Count ?? 0
    };
}
