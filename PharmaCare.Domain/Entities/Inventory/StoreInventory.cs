using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PharmaCare.Domain.Entities.Configuration;

namespace PharmaCare.Domain.Entities.Inventory;

/// <summary>
/// Store Inventory - Tracks stock levels per store per product.
/// </summary>
public class StoreInventory
{
    [Key]
    public int StoreInventoryID { get; set; }

    [ForeignKey("Store")]
    public int Store_ID { get; set; }
    public Store? Store { get; set; }

    [ForeignKey("Product")]
    public int Product_ID { get; set; }
    public Product? Product { get; set; }

    /// <summary>
    /// Current quantity on hand
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal QuantityOnHand { get; set; }

    /// <summary>
    /// Quantity reserved for pending transactions
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal ReservedQuantity { get; set; }

    /// <summary>
    /// Weighted average cost
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal AverageCost { get; set; }

    public DateTime LastUpdated { get; set; } = DateTime.Now;
}
