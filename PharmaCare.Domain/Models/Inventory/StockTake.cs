using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PharmaCare.Domain.Models.Base;
using PharmaCare.Domain.Models.Configuration;

namespace PharmaCare.Domain.Models.Inventory;

public class StockTake : BaseModel
{
    [Key]
    public int StockTakeID { get; set; }

    [ForeignKey("Store")]
    public int Store_ID { get; set; }
    public Store? Store { get; set; }

    public string Status { get; set; } = "Open"; // Open, Completed, Cancelled

    public string Remarks { get; set; } = string.Empty;

    // Created fields inherited from BaseModel
    public DateTime? CompletedDate { get; set; }
    public int? CompletedBy { get; set; }

    public List<StockTakeItem> StockTakeItems { get; set; } = new();
}

public class StockTakeItem
{
    [Key]
    public int StockTakeItemID { get; set; }

    [ForeignKey("StockTake")]
    public int StockTake_ID { get; set; }
    public StockTake? StockTake { get; set; }

    [ForeignKey("ProductBatch")]
    public int ProductBatch_ID { get; set; }
    public ProductBatch? ProductBatch { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal SystemQuantity { get; set; } // Snapshot at creation

    [Column(TypeName = "decimal(18, 2)")]
    public decimal PhysicalQuantity { get; set; } // Actual count

    // Calculated
    public decimal Variance => PhysicalQuantity - SystemQuantity;

    [Column(TypeName = "decimal(18, 2)")]
    public decimal VarianceCost { get; set; } // Cost * Variance
}
