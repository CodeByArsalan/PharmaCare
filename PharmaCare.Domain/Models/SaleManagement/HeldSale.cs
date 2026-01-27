using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PharmaCare.Domain.Models.Base;
using PharmaCare.Domain.Models.Configuration;

namespace PharmaCare.Domain.Models.SaleManagement;

/// <summary>
/// Represents a held/parked sale that can be resumed later
/// </summary>
public class HeldSale : BaseModel
{
    [Key]
    public int HeldSaleID { get; set; }

    [Required]
    [StringLength(50)]
    public string HoldNumber { get; set; } = string.Empty;

    [ForeignKey("Store")]
    public int Store_ID { get; set; }

    [ForeignKey("Party")]
    public int? Party_ID { get; set; }

    [StringLength(200)]
    public string? CustomerName { get; set; }

    [StringLength(50)]
    public string? CustomerPhone { get; set; }

    public DateTime HoldDate { get; set; }

    /// <summary>
    /// Auto-delete after this date
    /// </summary>
    public DateTime? ExpiryDate { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    /// <summary>
    /// Soft delete flag - false when retrieved or deleted
    /// </summary>
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public Store? Store { get; set; }
    public Party? Party { get; set; }
    public ICollection<HeldSaleLine> HeldLines { get; set; } = new List<HeldSaleLine>();
}
