using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PharmaCare.Domain.Entities.Base;
using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.Domain.Entities.Finance;

/// <summary>
/// Allocation record linking a payment/credit note to a specific sale invoice.
/// </summary>
public class PaymentAllocation : BaseEntity, ITenantEntity
{
    // Tenant (pharmacy) that owns this row. Auto-filtered and stamped by the DbContext.
    public int Pharmacy_ID { get; set; }

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

    public DateTime AllocationDate { get; set; } = AppTime.Now;

    [StringLength(20)]
    public string SourceType { get; set; } = "Receipt";

    [StringLength(500)]
    public string? Remarks { get; set; }
}
