using System.ComponentModel.DataAnnotations.Schema;
using PharmaCare.Domain.Models.Base;
using PharmaCare.Domain.Models.Inventory;
using PharmaCare.Domain.Models.Products;

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

    // Link to Original Purchase (StockMain with InvoiceType=2 for PURCHASE)
    [ForeignKey("StockMain")]
    public int? StockMain_ID { get; set; }
    public StockMain? StockMain { get; set; }

    // Navigation properties
    public Product? Product { get; set; }
    public ICollection<StoreInventory> StoreInventories { get; set; } = new List<StoreInventory>();

    [NotMapped]
    public decimal TotalQuantityOnHand => StoreInventories.Sum(si => si.QuantityOnHand);
}
