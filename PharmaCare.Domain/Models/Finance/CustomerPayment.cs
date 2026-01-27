using PharmaCare.Domain.Models.Base;
using PharmaCare.Domain.Models.AccountManagement;
using PharmaCare.Domain.Models.SaleManagement;
using PharmaCare.Domain.Models.Configuration;

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
    /// Optional: Link to a specific sale this payment is for
    /// </summary>
    public int? Sale_ID { get; set; }

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

    /// <summary>
    /// Linked journal entry for accounting
    /// </summary>
    public int? JournalEntry_ID { get; set; }

    // Navigation properties
    public Party? Party { get; set; }
    public Sale? Sale { get; set; }
    public JournalEntry? JournalEntry { get; set; }
}
