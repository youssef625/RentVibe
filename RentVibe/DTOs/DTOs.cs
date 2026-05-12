using System.ComponentModel.DataAnnotations;

namespace RentVibe.DTOs;


public class RegisterDto
{
    [Required] public string FullName { get; set; } = string.Empty;
    [Required] [EmailAddress] public string Email { get; set; } = string.Empty;
    [Required] [MinLength(6)] public string Password { get; set; } = string.Empty;
    [Required] public string Role { get; set; } = "Tenant"; 
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


public class CreatePropertyDto
{
    [Required] [MaxLength(200)] public string Title { get; set; } = string.Empty;
    [MaxLength(2000)] public string? Description { get; set; }
    [Required] [Range(1, 1_000_000_000, ErrorMessage = "Price must be between 1 and 1,000,000,000.")] public decimal Price { get; set; }
    [Required] [MaxLength(300)] public string Location { get; set; } = string.Empty;
    [Required] public string PropertyType { get; set; } = "Apartment";
    public bool HasParking { get; set; }
    public bool HasElevator { get; set; }
    public bool IsFurnished { get; set; }
    [Range(0, 100, ErrorMessage = "Bedrooms must be between 0 and 100.")] public int Bedrooms { get; set; }
    [Range(0, 100, ErrorMessage = "Bathrooms must be between 0 and 100.")] public int Bathrooms { get; set; }
    [Range(1, 100_000, ErrorMessage = "Area must be between 1 and 100,000 m².")] public double AreaSqFt { get; set; }
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


public class CreateVisitDto
{
    [Required] public int PropertyId { get; set; }
    [Required] public DateTime RequestedDate { get; set; }
    [MaxLength(500)] public string? Message { get; set; }
}


public class CreateApplicationDto
{
    [Required] public int PropertyId { get; set; }
    [Required] public DateTime RentalStartDate { get; set; }
    [Required] public DateTime RentalEndDate { get; set; }
    [MaxLength(2000)] public string? Message { get; set; }
}


public class CreateReviewDto
{
    [Required] public int PropertyId { get; set; }
    [Required] [Range(1, 5)] public int Rating { get; set; }
    [MaxLength(2000)] public string? Comment { get; set; }
}
