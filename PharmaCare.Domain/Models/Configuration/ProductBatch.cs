using System.ComponentModel.DataAnnotations.Schema;
using PharmaCare.Domain.Models.Base;
using PharmaCare.Domain.Models.Inventory;
using PharmaCare.Domain.Models.Products;
using PharmaCare.Domain.Models.PurchaseManagement;

namespace PharmaCare.Domain.Models.Configuration;

/// <summary>
/// Represents a specific batch of a product with expiry tracking
/// </summary>
public class ProductBatch : BaseModel
{
    public int ProductBatchID { get; set; }
    public int? Product_ID { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }

    // Batch Pricing
    public decimal CostPrice { get; set; } // Purchase cost per unit
    public decimal MRP { get; set; } // Maximum Retail Price
    public decimal SellingPrice { get; set; } // Current selling price

    // Link to Original GRN
    [ForeignKey("Grn")]
    public int? Grn_ID { get; set; }
    public Grn? Grn { get; set; }

    // Navigation properties
    public Product? Product { get; set; }
    public ICollection<StoreInventory> StoreInventories { get; set; } = new List<StoreInventory>();

    [NotMapped]
    public decimal TotalQuantityOnHand => StoreInventories.Sum(si => si.QuantityOnHand);
}
