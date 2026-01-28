using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PharmaCare.Domain.Models.Configuration;
using PharmaCare.Domain.Models.AccountManagement;
using PharmaCare.Domain.Models.Products;

namespace PharmaCare.Domain.Models.Inventory;

/// <summary>
/// Unified accounting voucher detail lines.
/// Replaces: JournalEntryLine
/// Uses Dr/Cr convention instead of DebitAmount/CreditAmount
/// </summary>
public class AccountVoucherDetail
{
    [Key]
    public int VoucherDetailID { get; set; }

    [ForeignKey("Voucher")]
    public int Voucher_ID { get; set; }
    public AccountVoucher? Voucher { get; set; }

    // ========== ACCOUNT ==========

    [ForeignKey("Account")]
    public int Account_ID { get; set; }
    public ChartOfAccount? Account { get; set; }

    // ========== DOUBLE ENTRY ==========

    /// <summary>
    /// Debit amount (left side of T-account)
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal Dr { get; set; }

    /// <summary>
    /// Credit amount (right side of T-account)
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal Cr { get; set; }

    // ========== OPTIONAL PRODUCT LINK ==========

    /// <summary>
    /// Optional product reference for detailed COGS tracking
    /// </summary>
    [ForeignKey("Product")]
    public int? Product_ID { get; set; }
    public Product? Product { get; set; }

    // ========== LINE DESCRIPTION ==========

    [MaxLength(200)]
    public string? Particulars { get; set; }

    // ========== STORE FOR MULTI-STORE REPORTING ==========

    [ForeignKey("Store")]
    public int? Store_ID { get; set; }
    public Store? Store { get; set; }
}
