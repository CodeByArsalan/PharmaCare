using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PharmaCare.Domain.Models.Base;
using PharmaCare.Domain.Models.Configuration;

namespace PharmaCare.Domain.Models.Inventory;

public class StockAdjustment : BaseModel
{
    [Key]
    public int StockAdjustmentID { get; set; }

    [Required]
    public DateTime AdjustmentDate { get; set; }

    [ForeignKey("Store")]
    public int Store_ID { get; set; }
    public Store? Store { get; set; }

    [ForeignKey("ProductBatch")]
    public int ProductBatch_ID { get; set; }
    public ProductBatch? ProductBatch { get; set; }

    [Required]
    [Column(TypeName = "decimal(18, 2)")]
    public decimal QuantityAdjusted { get; set; } // Negative for reduction, Positive for finding extra

    [Required]
    public string Reason { get; set; } = string.Empty; // e.g., "Expired", "Damaged", "Lost", "Found"

    public int AdjustedBy { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal FinancialImpact { get; set; } // Cost Price * Qty
}
