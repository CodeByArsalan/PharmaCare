namespace PharmaCare.Domain.Models.SaleManagement;

/// <summary>
/// Represents a payment for a sale (supports multiple payments per sale)
/// </summary>
public class Payment
{
    public int PaymentID { get; set; }
    public int? Sale_ID { get; set; }
    public string PaymentMethod { get; set; } = "Cash"; // Cash, Card, Mobile, Credit
    public decimal Amount { get; set; }
    public string? ReferenceNumber { get; set; }  // Card/mobile transaction reference
    public DateTime PaymentDate { get; set; } = DateTime.Now;

    // Navigation properties
    public Sale? Sale { get; set; }
}
