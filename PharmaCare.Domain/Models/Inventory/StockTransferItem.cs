using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PharmaCare.Domain.Models.Configuration;

namespace PharmaCare.Domain.Models.Inventory;

public class StockTransferItem
{
    [Key]
    public int StockTransferItemID { get; set; }

    [ForeignKey("StockTransfer")]
    public int StockTransfer_ID { get; set; }
    public StockTransfer? StockTransfer { get; set; }

    [ForeignKey("ProductBatch")]
    public int ProductBatch_ID { get; set; }
    public ProductBatch? ProductBatch { get; set; }

    [Required]
    [Column(TypeName = "decimal(18, 2)")]
    public decimal Quantity { get; set; }
}
