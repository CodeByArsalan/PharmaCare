using System.ComponentModel.DataAnnotations.Schema;
using PharmaCare.Domain.Models.Configuration;

namespace PharmaCare.Domain.Models.Inventory;

/// <summary>
/// Represents inventory levels for a specific batch at a specific store
/// </summary>
public class StoreInventory
{
    public int StoreInventoryID { get; set; }
    public int Store_ID { get; set; }
    public int ProductBatch_ID { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal QuantityOnHand { get; set; }

    // Navigation properties
    public Store? Store { get; set; }
    public ProductBatch? ProductBatch { get; set; }
}
