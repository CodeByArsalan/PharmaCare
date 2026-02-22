using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PharmaCare.Domain.Entities.Base;
using PharmaCare.Domain.Entities.Configuration;
using PharmaCare.Domain.Entities.Accounting;
using PharmaCare.Domain.Enums;
using PaymentStatusEnum = PharmaCare.Domain.Enums.PaymentStatus;

namespace PharmaCare.Domain.Entities.Transactions;

/// <summary>
/// Stock Main - Unified header for all inventory transactions.
/// Replaces: GRN, Sale, PurchaseReturn, SalesReturn, StockAdjustment
/// </summary>
public class StockMain : BaseEntity
{
    [Key]
    public int StockMainID { get; set; }

    // ========== TRANSACTION TYPE ==========
    [ForeignKey("TransactionType")]
    public int TransactionType_ID { get; set; }
    public TransactionType? TransactionType { get; set; }

    /// <summary>
    /// Auto-generated transaction number (e.g., GRN-2024-0001)
    /// </summary>
    [Required]
    [StringLength(50)]
    public string TransactionNo { get; set; } = string.Empty;

    public DateTime TransactionDate { get; set; } = DateTime.Now;

    // ========== PARTY ==========
    [ForeignKey("Party")]
    public int? Party_ID { get; set; }
    public Party? Party { get; set; }

    // ========== FINANCIAL ==========
    [Column(TypeName = "decimal(18,2)")]
    public decimal SubTotal { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal DiscountPercent { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal PaidAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal BalanceAmount { get; set; }

    // ========== STATUS ==========
    /// <summary>
    /// Draft, Approved, Void
    /// </summary>
    [StringLength(20)]
    public string Status { get; set; } = TransactionStatus.Draft.ToString();

    /// <summary>
    /// Unpaid, Partial, Paid
    /// </summary>
    [StringLength(20)]
    public string PaymentStatus { get; set; } = PaymentStatusEnum.Unpaid.ToString();

    // ========== ACCOUNTING LINK ==========
    [ForeignKey("Voucher")]
    public int? Voucher_ID { get; set; }
    public Voucher? Voucher { get; set; }

    // ========== REFERENCE (FOR RETURNS) ==========
    /// <summary>
    /// Links to original transaction (for returns)
    /// </summary>
    [ForeignKey("ReferenceStockMain")]
    public int? ReferenceStockMain_ID { get; set; }
    public StockMain? ReferenceStockMain { get; set; }



    // ========== REMARKS ==========
    [StringLength(500)]
    public string? Remarks { get; set; }

    // ========== VOID TRACKING ==========
    [StringLength(500)]
    public string? VoidReason { get; set; }
    public int? VoidedBy { get; set; }
    public DateTime? VoidedAt { get; set; }

    // ========== NAVIGATION ==========
    public ICollection<StockDetail> StockDetails { get; set; } = new List<StockDetail>();
}
