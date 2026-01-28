using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PharmaCare.Domain.Entities.Configuration;

namespace PharmaCare.Domain.Entities.Transactions;

/// <summary>
/// Voucher - Accounting voucher header.
/// All financial transactions create vouchers for double-entry bookkeeping.
/// </summary>
public class Voucher
{
    [Key]
    public int VoucherID { get; set; }

    [ForeignKey("VoucherType")]
    public int VoucherType_ID { get; set; }
    public VoucherType? VoucherType { get; set; }

    /// <summary>
    /// Auto-generated voucher number (e.g., JV-2024-0001)
    /// </summary>
    [Required]
    [StringLength(50)]
    public string VoucherNo { get; set; } = string.Empty;

    public DateTime VoucherDate { get; set; } = DateTime.Now;

    [ForeignKey("Store")]
    public int? Store_ID { get; set; }
    public Store? Store { get; set; }

    // ========== TOTALS (must balance) ==========
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalDebit { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalCredit { get; set; }

    // ========== STATUS ==========
    /// <summary>
    /// Posted, Reversed
    /// </summary>
    [StringLength(20)]
    public string Status { get; set; } = "Posted";

    // ========== SOURCE REFERENCE ==========
    /// <summary>
    /// Source table name: StockMain, Expense, Payment
    /// </summary>
    [StringLength(50)]
    public string? SourceTable { get; set; }

    /// <summary>
    /// Source record ID
    /// </summary>
    public int? SourceID { get; set; }

    [StringLength(500)]
    public string? Narration { get; set; }

    // ========== REVERSAL TRACKING ==========
    public bool IsReversed { get; set; }

    [ForeignKey("ReversedByVoucher")]
    public int? ReversedByVoucher_ID { get; set; }
    public Voucher? ReversedByVoucher { get; set; }

    [ForeignKey("ReversesVoucher")]
    public int? ReversesVoucher_ID { get; set; }
    public Voucher? ReversesVoucher { get; set; }

    // ========== AUDIT ==========
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public int CreatedBy { get; set; }

    // ========== NAVIGATION ==========
    public ICollection<VoucherDetail> VoucherDetails { get; set; } = new List<VoucherDetail>();
}
