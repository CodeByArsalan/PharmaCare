
using PharmaCare.Domain.Models.Base;
using PharmaCare.Domain.Models.Configuration;
using PharmaCare.Domain.Models.Inventory;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PharmaCare.Domain.Models.Finance;

/// <summary>
/// Represents a payment made to a supplier for goods received
/// </summary>
public class SupplierPayment : BaseModelWithStatus
{
    [Key]
    public int SupplierPaymentID { get; set; }

    /// <summary>
    /// Payment reference number (SP-2026-000001)
    /// </summary>
    [Required]
    [StringLength(50)]
    [Display(Name = "Payment Number")]
    public string PaymentNumber { get; set; } = string.Empty;

    /// <summary>
    /// Supplier being paid
    /// </summary>
    [Required]
    [ForeignKey("Party")]
    [Display(Name = "Supplier")]
    public int Party_ID { get; set; }
    public virtual Party? Party { get; set; }

    /// <summary>
    /// Purchase transaction (StockMain with InvoiceType=2) being paid for
    /// </summary>
    [Required]
    [ForeignKey("StockMain")]
    [Display(Name = "Purchase")]
    public int StockMain_ID { get; set; }
    public virtual StockMain? StockMain { get; set; }

    /// <summary>
    /// Date of payment
    /// </summary>
    [Required]
    [Display(Name = "Payment Date")]
    public DateTime PaymentDate { get; set; } = DateTime.Now;

    /// <summary>
    /// Total purchase amount at time of payment
    /// </summary>
    [Column(TypeName = "decimal(18, 2)")]
    [Display(Name = "Purchase Amount")]
    public decimal GrnAmount { get; set; }

    /// <summary>
    /// Amount paid in this transaction
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(18, 2)")]
    [Display(Name = "Amount Paid")]
    public decimal AmountPaid { get; set; }

    /// <summary>
    /// Balance remaining after this payment
    /// </summary>
    [Column(TypeName = "decimal(18, 2)")]
    [Display(Name = "Balance After")]
    public decimal BalanceAfter { get; set; }

    /// <summary>
    /// Payment type: Full or Partial
    /// </summary>
    [Required]
    [StringLength(20)]
    [Display(Name = "Payment Type")]
    public string PaymentType { get; set; } = "Full"; // Full, Partial

    /// <summary>
    /// Payment method: Cash, Bank, Cheque
    /// </summary>
    [Required]
    [StringLength(20)]
    [Display(Name = "Payment Method")]
    public string PaymentMethod { get; set; } = "Cash"; // Cash, Bank, Cheque

    /// <summary>
    /// Cheque number if payment by cheque
    /// </summary>
    [StringLength(100)]
    [Display(Name = "Cheque Number")]
    public string? ChequeNumber { get; set; }

    /// <summary>
    /// Cheque date if payment by cheque
    /// </summary>
    [Display(Name = "Cheque Date")]
    public DateTime? ChequeDate { get; set; }

    /// <summary>
    /// Bank reference/transaction number
    /// </summary>
    [StringLength(100)]
    [Display(Name = "Bank Reference")]
    public string? BankReference { get; set; }

    /// <summary>
    /// Payment status: Paid, Cancelled
    /// </summary>
    [Required]
    [StringLength(20)]
    public string Status { get; set; } = "Paid";



    /// <summary>
    /// Link to Account Voucher (Replaces JournalEntry)
    /// </summary>
    [ForeignKey("AccountVoucher")]
    public int? Voucher_ID { get; set; }
    public virtual AccountVoucher? AccountVoucher { get; set; }

    /// <summary>
    /// Store where payment was made
    /// </summary>
    [ForeignKey("Store")]
    public int? Store_ID { get; set; }
    public virtual Store? Store { get; set; }

    /// <summary>
    /// Additional notes
    /// </summary>
    [StringLength(500)]
    public string? Notes { get; set; }
}
