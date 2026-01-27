using PharmaCare.Domain.Models.Base;
using PharmaCare.Domain.Models.Prescriptions;
using PharmaCare.Domain.Models.Configuration;

namespace PharmaCare.Domain.Models.SaleManagement;

/// <summary>
/// Represents a sale transaction
/// </summary>
public class Sale : BaseModel
{
    public int SaleID { get; set; }
    public string SaleNumber { get; set; } = string.Empty;
    public DateTime SaleDate { get; set; }
    public int Store_ID { get; set; }
    public int? Party_ID { get; set; }
    public int? Prescription_ID { get; set; }
    public string Status { get; set; } = "Completed"; // Completed, Voided, Returned

    // ========== AMOUNTS ==========
    public decimal SubTotal { get; set; }           // Before discount
    public decimal DiscountPercent { get; set; }    // Invoice discount %
    public decimal DiscountAmount { get; set; }     // Total discount amount
    public decimal Total { get; set; }              // Final amount (SubTotal - Discount)

    // ========== PAYMENT TRACKING ==========
    public string PaymentStatus { get; set; } = "Paid"; // Paid, Credit, Partial
    public decimal AmountPaid { get; set; }         // Amount received
    public decimal BalanceAmount { get; set; }      // Outstanding balance (for credit)

    // ========== VOID TRACKING ==========
    public string? VoidReason { get; set; }
    public int? VoidedBy { get; set; }
    public DateTime? VoidedDate { get; set; }

    // Foreign key to journal entry (for double-entry accounting)
    public int? JournalEntry_ID { get; set; }
    
    // Error tracking for accounting integration
    public string? AccountingError { get; set; }

    // Navigation properties
    public Store? Store { get; set; }
    public Party? Party { get; set; }
    public Prescription? Prescription { get; set; }
    public ICollection<SaleLine> SaleLines { get; set; } = new List<SaleLine>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

