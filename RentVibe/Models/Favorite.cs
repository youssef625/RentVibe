using System.ComponentModel.DataAnnotations.Schema;

namespace RentVibe.Models;

public class Favorite
{
    public string TenantId { get; set; } = string.Empty;

    [ForeignKey(nameof(TenantId))]
    public ApplicationUser Tenant { get; set; } = null!;

    public int PropertyId { get; set; }

    [ForeignKey(nameof(PropertyId))]
    public Property Property { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
