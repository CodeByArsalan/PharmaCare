using System.ComponentModel.DataAnnotations;
using PharmaCare.Domain.Entities.Base;

namespace PharmaCare.Domain.Entities.Configuration;

/// <summary>
/// Store/Branch entity representing business locations.
/// </summary>
public class Store : BaseEntityWithStatus
{
    [Key]
    public int StoreID { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Address { get; set; }

    [StringLength(20)]
    public string? Phone { get; set; }


}
