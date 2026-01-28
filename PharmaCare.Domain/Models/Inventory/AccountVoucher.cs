using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PharmaCare.Domain.Models.Configuration;
using PharmaCare.Domain.Models.AccountManagement;

namespace PharmaCare.Domain.Models.Inventory;

/// <summary>
/// Unified accounting voucher header.
/// Replaces: JournalEntry
/// </summary>
public class AccountVoucher
{
    [Key]
    public int VoucherID { get; set; }

    [ForeignKey("VoucherType")]
    public int VoucherType_ID { get; set; }
    public AccountVoucherType? VoucherType { get; set; }

    [Required]
    [MaxLength(50)]
    public string VoucherCode { get; set; } = string.Empty;

    public DateTime VoucherDate { get; set; } = DateTime.Now;

    // ========== SOURCE REFERENCE ==========

    /// <summary>
    /// Source table name: StockMain, Expense, etc.
    /// </summary>
    [MaxLength(50)]
    public string? SourceTable { get; set; }

    /// <summary>
    /// Source record ID (StockMainID, ExpenseID, etc.)
    /// </summary>
    public int? SourceID { get; set; }

    // ========== CONTEXT ==========

    [ForeignKey("Store")]
    public int? Store_ID { get; set; }
    public Store? Store { get; set; }

    [ForeignKey("FiscalPeriod")]
    public int? FiscalPeriod_ID { get; set; }
    public FiscalPeriod? FiscalPeriod { get; set; }

    // ========== TOTALS (must balance) ==========

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalDebit { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalCredit { get; set; }

    // ========== STATUS ==========

    [MaxLength(20)]
    public string Status { get; set; } = "Posted";

    public bool IsReversed { get; set; }

    [ForeignKey("ReversedByVoucher")]
    public int? ReversedBy_ID { get; set; }
    public AccountVoucher? ReversedByVoucher { get; set; }

    [ForeignKey("ReversesVoucher")]
    public int? Reverses_ID { get; set; }
    public AccountVoucher? ReversesVoucher { get; set; }

    // ========== AUDIT ==========

    [MaxLength(500)]
    public string? Narration { get; set; }

    public int CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.Now;

    // ========== NAVIGATION ==========

    public ICollection<AccountVoucherDetail> VoucherDetails { get; set; } = new List<AccountVoucherDetail>();
}
