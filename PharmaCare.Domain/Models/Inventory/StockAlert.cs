using PharmaCare.Domain.Models.Base;
using PharmaCare.Domain.Models.Configuration;
using PharmaCare.Domain.Models.Products;
using System.ComponentModel.DataAnnotations;

namespace PharmaCare.Domain.Models.Inventory;

/// <summary>
/// Represents inventory alerts for low stock, expiring items, etc.
/// </summary>
public class StockAlert : BaseModel
{
    [Key]
    public int StockAlertID { get; set; }

    public int? Product_ID { get; set; }
    public int? Store_ID { get; set; }

    [Required]
    [MaxLength(50)]
    public string AlertType { get; set; } = string.Empty; // LowStock, Expiring, OutOfStock

    [MaxLength(20)]
    public string Severity { get; set; } = "Medium"; // Low, Medium, High, Critical

    [Required]
    [MaxLength(500)]
    public string Message { get; set; } = string.Empty;

    public bool IsResolved { get; set; }
    public DateTime? ResolvedDate { get; set; }
    public int? ResolvedBy { get; set; }

    // Navigation properties
    public Product? Product { get; set; }
    public Store? Store { get; set; }
}
