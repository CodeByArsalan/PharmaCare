using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PharmaCare.Domain.Entities.Base;
using PharmaCare.Domain.Entities.Configuration;
using PharmaCare.Domain.Entities.Transactions;
using PharmaCare.Domain.Entities.Accounting;

namespace PharmaCare.Domain.Entities.Finance;

/// <summary>
/// Customer credit note generated from overpaid sales / sales returns.
/// </summary>
public class CreditNote : BaseEntity
{
    [Key]
    public int CreditNoteID { get; set; }

    [Required]
    [StringLength(50)]
    public string CreditNoteNo { get; set; } = string.Empty;

    [ForeignKey("Party")]
    public int Party_ID { get; set; }
    public Party? Party { get; set; }

    /// <summary>
    /// Source transaction creating this credit note (usually sale return).
    /// </summary>
    [ForeignKey("SourceStockMain")]
    public int? SourceStockMain_ID { get; set; }
    public StockMain? SourceStockMain { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal AppliedAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal BalanceAmount { get; set; }

    public DateTime CreditDate { get; set; } = DateTime.Now;

    /// <summary>
    /// Open, Applied, Void
    /// </summary>
    [StringLength(20)]
    public string Status { get; set; } = "Open";

    [StringLength(500)]
    public string? Remarks { get; set; }

    [StringLength(500)]
    public string? VoidReason { get; set; }
    public int? VoidedBy { get; set; }
    public DateTime? VoidedAt { get; set; }

    [ForeignKey("Voucher")]
    public int? Voucher_ID { get; set; }
    public Voucher? Voucher { get; set; }

    public ICollection<PaymentAllocation> PaymentAllocations { get; set; } = new List<PaymentAllocation>();
}
