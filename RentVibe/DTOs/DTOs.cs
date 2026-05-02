using System.ComponentModel.DataAnnotations;

namespace RentVibe.DTOs;

// ---- Auth DTOs ----
public class RegisterDto
{
    [Required] public string FullName { get; set; } = string.Empty;
    [Required] [EmailAddress] public string Email { get; set; } = string.Empty;
    [Required] [MinLength(6)] public string Password { get; set; } = string.Empty;
    [Required] public string Role { get; set; } = "Tenant"; // "Tenant" or "Landlord"
}

public class LoginDto
{
    [Required] [EmailAddress] public string Email { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
}

public class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

// ---- Property DTOs ----
public class CreatePropertyDto
{
    [Required] [MaxLength(200)] public string Title { get; set; } = string.Empty;
    [MaxLength(2000)] public string? Description { get; set; }
    public decimal Price { get; set; }
    [Required] [MaxLength(300)] public string Location { get; set; } = string.Empty;
    public string PropertyType { get; set; } = "Apartment";
    public bool HasParking { get; set; }
    public bool HasElevator { get; set; }
    public bool IsFurnished { get; set; }
    public int Bedrooms { get; set; }
    public int Bathrooms { get; set; }
    public double AreaSqFt { get; set; }
}

public class UpdatePropertyDto : CreatePropertyDto { }

public class PropertyResponseDto
{
    public int Id { get; set; }
    public string LandlordId { get; set; } = string.Empty;
    public string LandlordName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string Location { get; set; } = string.Empty;
    public string PropertyType { get; set; } = string.Empty;
    public bool HasParking { get; set; }
    public bool HasElevator { get; set; }
    public bool IsFurnished { get; set; }
    public string RentalStatus { get; set; } = string.Empty;
    public string ApprovalStatus { get; set; } = string.Empty;
    public int Bedrooms { get; set; }
    public int Bathrooms { get; set; }
    public double AreaSqFt { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<string> ImageUrls { get; set; } = new();
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }
}

// ---- Visit DTOs ----
public class CreateVisitDto
{
    [Required] public int PropertyId { get; set; }
    [Required] public DateTime RequestedDate { get; set; }
    [MaxLength(500)] public string? Message { get; set; }
}

// ---- RentalApplication DTOs ----
public class CreateApplicationDto
{
    [Required] public int PropertyId { get; set; }
    [MaxLength(2000)] public string? Message { get; set; }
}

// ---- Review DTOs ----
public class CreateReviewDto
{
    [Required] public int PropertyId { get; set; }
    [Required] [Range(1, 5)] public int Rating { get; set; }
    [MaxLength(2000)] public string? Comment { get; set; }
}
