using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PharmaCare.Domain.Entities.Base;
using PharmaCare.Domain.Entities.Configuration;
using PharmaCare.Domain.Entities.Transactions;
using PharmaCare.Domain.Entities.Accounting;

namespace PharmaCare.Domain.Entities.Finance;

/// <summary>
/// Records a credit note issued by a supplier (e.g. for damaged / wrong goods returned to supplier).
/// Does NOT affect stock — purely a financial adjustment reducing what we owe the supplier.
/// </summary>
public class SupplierCreditNote : BaseEntity, ITenantEntity
{
    // Tenant (pharmacy) that owns this row. Auto-filtered and stamped by the DbContext.
    public int Pharmacy_ID { get; set; }

    [Key]
    public int SupplierCreditNoteID { get; set; }

    [Required, StringLength(50)]
    public string CreditNoteNo { get; set; } = string.Empty;

    [ForeignKey("Party")]
    public int Party_ID { get; set; }
    public Party? Party { get; set; }

    /// <summary>Optional GRN this credit note was raised against.</summary>
    [ForeignKey("SourceStockMain")]
    public int? SourceStockMain_ID { get; set; }
    public StockMain? SourceStockMain { get; set; }

    /// <summary>
    /// Contra (adjustment) account credited when the note is posted — e.g. Purchase Returns &amp;
    /// Allowances, rebate / other income. This is the offset to the supplier payable debit so the
    /// voucher produces real ledger movement (not a self-cancelling supplier DR/CR).
    /// </summary>
    [ForeignKey("AdjustmentAccount")]
    public int? AdjustmentAccount_ID { get; set; }
    public Account? AdjustmentAccount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal AppliedAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    [ConcurrencyCheck]
    public decimal BalanceAmount { get; set; }

    public DateTime CreditDate { get; set; } = AppTime.Now;

    /// <summary>Open | Applied | Void</summary>
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
}
