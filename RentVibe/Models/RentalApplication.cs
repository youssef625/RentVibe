using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RentVibe.Models.Enums;

namespace RentVibe.Models;

public class RentalApplication
{
    public int Id { get; set; }

    public int PropertyId { get; set; }

    [ForeignKey(nameof(PropertyId))]
    public Property Property { get; set; } = null!;

    public string TenantId { get; set; } = string.Empty;

    [ForeignKey(nameof(TenantId))]
    public ApplicationUser Tenant { get; set; } = null!;

    [MaxLength(2000)]
    public string? Message { get; set; }

    public ApplicationStatus Status { get; set; } = ApplicationStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<ApplicationDocument> Documents { get; set; } = new List<ApplicationDocument>();
}
