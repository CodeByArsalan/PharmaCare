using PharmaCare.Domain.Models.Base;

using PharmaCare.Domain.Models.Configuration;
using PharmaCare.Domain.Models.Inventory;
using System.ComponentModel.DataAnnotations.Schema;

namespace PharmaCare.Domain.Models.Finance;

/// <summary>
/// Represents a payment received from a customer for a credit sale.
/// </summary>
public class CustomerPayment : BaseModel
{
    public int CustomerPaymentID { get; set; }

    /// <summary>
    /// Auto-generated payment number (CPAY-yyyyMMddHHmmssfff)
    /// </summary>
    public string PaymentNumber { get; set; } = string.Empty;

    public DateTime PaymentDate { get; set; }

    /// <summary>
    /// The customer (Party) making the payment
    /// </summary>
    public int Party_ID { get; set; }

    /// <summary>
    /// Optional: Link to a specific sale (StockMain with InvoiceType=1) this payment is for
    /// </summary>
    public int? StockMain_ID { get; set; }

    /// <summary>
    /// Payment amount
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Payment method: Cash, Bank, Card, etc.
    /// </summary>
    public string PaymentMethod { get; set; } = "Cash";

    /// <summary>
    /// Reference number for bank transfers, checks, etc.
    /// </summary>
    public string? ReferenceNumber { get; set; }

    /// <summary>
    /// Optional notes about the payment
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Status: Active, Cancelled
    /// </summary>
    public string Status { get; set; } = "Active";

    /// <summary>
    /// Soft delete flag
    /// </summary>
    public bool IsActive { get; set; } = true;



    // Navigation properties
    public Party? Party { get; set; }
    public StockMain? StockMain { get; set; }

    
    /// <summary>
    /// Link to Account Voucher (Replaces JournalEntry)
    /// </summary>
    [ForeignKey("AccountVoucher")]
    public int? Voucher_ID { get; set; }
    public AccountVoucher? AccountVoucher { get; set; }
}
