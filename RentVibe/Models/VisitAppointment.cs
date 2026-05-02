using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RentVibe.Models.Enums;

namespace RentVibe.Models;

public class VisitAppointment
{
    public int Id { get; set; }

    public int PropertyId { get; set; }

    [ForeignKey(nameof(PropertyId))]
    public Property Property { get; set; } = null!;

    public string TenantId { get; set; } = string.Empty;

    [ForeignKey(nameof(TenantId))]
    public ApplicationUser Tenant { get; set; } = null!;

    public DateTime RequestedDate { get; set; }

    [MaxLength(500)]
    public string? Message { get; set; }

    public VisitStatus Status { get; set; } = VisitStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
