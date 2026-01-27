namespace PharmaCare.Web.Models.POS;

/// <summary>
/// Receipt data after successful sale
/// </summary>
public class ReceiptViewModel
{
    public int SaleID { get; set; }  // Matches SaleID from database
    public string SaleNumber { get; set; } = string.Empty;
    public DateTime SaleDate { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public List<ReceiptItemViewModel> Items { get; set; } = new();
    public List<ReceiptPaymentViewModel> Payments { get; set; } = new();
    public decimal SubTotal { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal Total { get; set; }
    public decimal AmountPaid => Payments.Sum(p => p.Amount);
    public decimal Change => AmountPaid - Total;
}

public class ReceiptItemViewModel
{
    public string ProductName { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal Subtotal { get; set; }
}

public class ReceiptPaymentViewModel
{
    public string PaymentMethod { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}
