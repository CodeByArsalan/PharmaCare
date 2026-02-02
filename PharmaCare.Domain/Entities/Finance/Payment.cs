using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PharmaCare.Domain.Entities.Base;
using PharmaCare.Domain.Entities.Accounting;
using PharmaCare.Domain.Entities.Configuration;
using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.Domain.Entities.Finance;

/// <summary>
/// Payment - Customer receipts and supplier payments.
/// </summary>
public class Payment : BaseEntity
{
    [Key]
    public int PaymentID { get; set; }

    /// <summary>
    /// RECEIPT = Money from customer, PAYMENT = Money to supplier
    /// </summary>
    [Required]
    [StringLength(20)]
    public string PaymentType { get; set; } = "RECEIPT";

    [ForeignKey("Party")]
    public int Party_ID { get; set; }
    public Party? Party { get; set; }

    /// <summary>
    /// Optional link to specific transaction being settled
    /// </summary>
    [ForeignKey("StockMain")]
    public int? StockMain_ID { get; set; }
    public StockMain? StockMain { get; set; }

    /// <summary>
    /// Cash/Bank account used for payment
    /// </summary>
    [ForeignKey("Account")]
    public int Account_ID { get; set; }
    public Account? Account { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    public DateTime PaymentDate { get; set; } = DateTime.Now;

    /// <summary>
    /// Cash, Bank, Cheque
    /// </summary>
    [StringLength(20)]
    public string PaymentMethod { get; set; } = "Cash";

    [StringLength(100)]
    public string? Reference { get; set; }

    [StringLength(50)]
    public string? ChequeNo { get; set; }

    public DateTime? ChequeDate { get; set; }

    [StringLength(500)]
    public string? Remarks { get; set; }

    /// <summary>
    /// Link to auto-generated accounting voucher
    /// </summary>
    [ForeignKey("Voucher")]
    public int? Voucher_ID { get; set; }
    public Voucher? Voucher { get; set; }
}
