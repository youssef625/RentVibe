using System.Security.Claims;
using Path = System.IO.Path;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RentVibe.Data.Repositories;
using RentVibe.DTOs;
using RentVibe.Models;
using RentVibe.Models.Enums;

namespace RentVibe.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public class PropertiesController : ControllerBase
{
    private readonly PropertyRepository _properties;
    private readonly DataRepository<Property> _propertyRepo;
    private readonly DataRepository<PropertyImage> _imageRepo;
    private readonly IWebHostEnvironment _env;

    public PropertiesController(
        PropertyRepository properties,
        DataRepository<Property> propertyRepo,
        DataRepository<PropertyImage> imageRepo,
        IWebHostEnvironment env)
    {
        _properties = properties;
        _propertyRepo = propertyRepo;
        _imageRepo = imageRepo;
        _env = env;
    }

    
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? location,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] string? propertyType)
    {
        PropertyType? parsedType = null;
        if (!string.IsNullOrWhiteSpace(propertyType)
            && Enum.TryParse<PropertyType>(propertyType, true, out var pt))
        {
            parsedType = pt;
        }

        var properties = await _properties.GetApprovedAvailableAsync(
            search,
            location,
            minPrice,
            maxPrice,
            parsedType);

        return Ok(properties.Select(MapToDto).ToList());
    }

    
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var property = await _properties.GetByIdWithDetailsAsync(id);

        if (property is null) return NotFound();
        return Ok(MapToDto(property));
    }

    
    [HttpGet("my")]
    [Authorize(Policy = "LandlordOnly")]
    public async Task<IActionResult> GetMyProperties()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var properties = await _properties.GetByLandlordAsync(userId);

        return Ok(properties.Select(MapToDto).ToList());
    }

    
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

        await _propertyRepo.AddAsync(property);

        return CreatedAtAction(nameof(GetById), new { id = property.Id }, new { property.Id });
    }

    
    [HttpPut("{id:int}")]
    [Authorize(Policy = "LandlordOnly")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdatePropertyDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var property = await _properties.GetByIdForLandlordAsync(id, userId);
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

        await _propertyRepo.UpdateAsync(property);
        return Ok(new { message = "Property updated." });
    }

    
    [HttpDelete("{id:int}")]
    [Authorize(Policy = "LandlordOnly")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var property = await _properties.GetByIdForLandlordAsync(id, userId);
        if (property is null) return NotFound();

        await _propertyRepo.DeleteAsync(property);
        return NoContent();
    }

    
    [HttpPost("{id:int}/images")]
    [Authorize(Policy = "LandlordOnly")]
    public async Task<IActionResult> UploadImages(int id, [FromForm] List<IFormFile> files)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var property = await _properties.GetByIdForLandlordAsync(id, userId);
        if (property is null) return NotFound();

        var uploadDir = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads", "properties");
        Directory.CreateDirectory(uploadDir);

        var urls = new List<string>();
        var images = new List<PropertyImage>();
        foreach (var file in files)
        {
            if (file.Length == 0) continue;
            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadDir, fileName);

            await using var stream = new System.IO.FileStream(filePath, System.IO.FileMode.Create);
            await file.CopyToAsync(stream);

            var url = $"/uploads/properties/{fileName}";
            images.Add(new PropertyImage { PropertyId = id, ImageUrl = url });
            urls.Add(url);
        }

        if (images.Count > 0)
        {
            await _properties.AddImagesAsync(images);
        }
        return Ok(new { imageUrls = urls });
    }

    [HttpDelete("{propertyId:int}/images")]
    [Authorize(Policy = "LandlordOnly")]
    public async Task<IActionResult> DeleteImage(int propertyId, [FromQuery] string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return BadRequest(new { message = "imageUrl is required." });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var image = await _properties.GetImageForLandlordAsync(propertyId, imageUrl, userId);

        if (image is null) return NotFound();

        var filePath = Path.Combine(_env.WebRootPath ?? "wwwroot", image.ImageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (System.IO.File.Exists(filePath))
            System.IO.File.Delete(filePath);

        await _imageRepo.DeleteAsync(image);
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
