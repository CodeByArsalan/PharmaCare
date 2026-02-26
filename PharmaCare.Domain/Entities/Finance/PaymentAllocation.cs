using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PharmaCare.Domain.Entities.Base;
using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.Domain.Entities.Finance;

/// <summary>
/// Allocation record linking a payment/credit note to a specific sale invoice.
/// </summary>
public class PaymentAllocation : BaseEntity
{
    [Key]
    public int PaymentAllocationID { get; set; }

    [ForeignKey("Payment")]
    public int? Payment_ID { get; set; }
    public Payment? Payment { get; set; }

    [ForeignKey("CreditNote")]
    public int? CreditNote_ID { get; set; }
    public CreditNote? CreditNote { get; set; }

    [ForeignKey("StockMain")]
    public int StockMain_ID { get; set; }
    public StockMain? StockMain { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    public DateTime AllocationDate { get; set; } = DateTime.Now;

    [StringLength(20)]
    public string SourceType { get; set; } = "Receipt";

    [StringLength(500)]
    public string? Remarks { get; set; }
}
