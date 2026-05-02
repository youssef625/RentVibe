using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RentVibe.Models;

public class ApplicationDocument
{
    public int Id { get; set; }

    public int RentalApplicationId { get; set; }

    [ForeignKey(nameof(RentalApplicationId))]
    public RentalApplication RentalApplication { get; set; } = null!;

    [Required]
    [MaxLength(500)]
    public string DocumentUrl { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? FileName { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
