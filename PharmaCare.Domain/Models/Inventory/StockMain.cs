using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PharmaCare.Domain.Models.Base;
using PharmaCare.Domain.Models.Configuration;
using PharmaCare.Domain.Models.AccountManagement;

namespace PharmaCare.Domain.Models.Inventory;

/// <summary>
/// Unified header table for all inventory transactions.
/// Replaces: Grn, PurchaseOrder, PurchaseReturn, Sale, SalesReturn, StockTransfer, StockTake
/// </summary>
public class StockMain : BaseModel
{
    [Key]
    public int StockMainID { get; set; }

    // ========== TRANSACTION TYPE ==========

    [ForeignKey("InvoiceType")]
    public int InvoiceType_ID { get; set; }
    public InvoiceType? InvoiceType { get; set; }

    [Required]
    [MaxLength(50)]
    public string InvoiceNo { get; set; } = string.Empty;

    public DateTime InvoiceDate { get; set; } = DateTime.Now;

    // ========== LOCATION & PARTY ==========

    [ForeignKey("Store")]
    public int Store_ID { get; set; }
    public Store? Store { get; set; }

    [ForeignKey("Party")]
    public int? Party_ID { get; set; }
    public Party? Party { get; set; }

    // ========== FINANCIAL ==========

    [Column(TypeName = "decimal(18,2)")]
    public decimal SubTotal { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal? DiscountPercent { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal PaidAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal BalanceAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ReturnedAmount { get; set; }

    // ========== STATUS & WORKFLOW ==========

    [MaxLength(20)]
    public string Status { get; set; } = "Draft";

    [MaxLength(20)]
    public string? PaymentStatus { get; set; }

    // ========== ACCOUNTING LINK (CRITICAL) ==========

    /// <summary>
    /// Link to accounting voucher (payment/receipt)
    /// </summary>
    [ForeignKey("PaymentVoucher")]
    public int? PaymentVoucher_ID { get; set; }
    public AccountVoucher? PaymentVoucher { get; set; }

    /// <summary>
    /// Cash/Bank account used for payment
    /// </summary>
    [ForeignKey("Account")]
    public int? Account_ID { get; set; }
    public ChartOfAccount? Account { get; set; }

    // ========== REFERENCE TO ORIGINAL (FOR RETURNS) ==========

    /// <summary>
    /// For returns: links to original sale/purchase
    /// For transfers: links paired IN/OUT transactions
    /// </summary>
    [ForeignKey("ReferenceStockMain")]
    public int? ReferenceStockMain_ID { get; set; }
    public StockMain? ReferenceStockMain { get; set; }

    // ========== TYPE-SPECIFIC FIELDS ==========

    [MaxLength(500)]
    public string? Remarks { get; set; }

    // GRN-specific
    [MaxLength(50)]
    public string? SupplierInvoiceNo { get; set; }

    // Void tracking (for sales)
    [MaxLength(500)]
    public string? VoidReason { get; set; }
    public int? VoidedBy { get; set; }
    public DateTime? VoidedDate { get; set; }

    // Return-specific
    [MaxLength(50)]
    public string? ReturnReason { get; set; }
    [MaxLength(500)]
    public string? ReturnNotes { get; set; }
    [MaxLength(20)]
    public string? RefundMethod { get; set; }
    [MaxLength(20)]
    public string? RefundStatus { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal RefundAmount { get; set; }

    [ForeignKey("RefundVoucher")]
    public int? RefundVoucher_ID { get; set; }
    public AccountVoucher? RefundVoucher { get; set; }

    // Transfer-specific
    [ForeignKey("DestinationStore")]
    public int? DestinationStore_ID { get; set; }
    public Store? DestinationStore { get; set; }

    public int? ReceivedBy { get; set; }
    public DateTime? ReceivedDate { get; set; }

    // Stock Take-specific
    public DateTime? CompletedDate { get; set; }
    public int? CompletedBy { get; set; }

    // Purchase Order-specific
    public DateTime? ExpectedDeliveryDate { get; set; }

    // ========== NAVIGATION ==========

    public ICollection<StockDetail> StockDetails { get; set; } = new List<StockDetail>();
}
