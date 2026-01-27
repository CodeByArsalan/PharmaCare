using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PharmaCare.Domain.Models.Base;
using PharmaCare.Domain.Models.Configuration;

namespace PharmaCare.Domain.Models.Inventory;

public class StockTransfer : BaseModel
{
    [Key]
    public int StockTransferID { get; set; }

    [Required]
    [MaxLength(50)]
    public string TransferNumber { get; set; } = string.Empty;

    [Required]
    public DateTime TransferDate { get; set; } = DateTime.Now;

    [ForeignKey("SourceStore")]
    public int SourceStore_ID { get; set; }
    public Store? SourceStore { get; set; }

    [ForeignKey("DestinationStore")]
    public int DestinationStore_ID { get; set; }
    public Store? DestinationStore { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Pending"; // Pending, Completed, Cancelled

    public string? Remarks { get; set; } = string.Empty;

    public int? ReceivedBy { get; set; }
    public DateTime? ReceivedDate { get; set; }

    public ICollection<StockTransferItem> StockTransferItems { get; set; } = new List<StockTransferItem>();
}
