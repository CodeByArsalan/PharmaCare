namespace PharmaCare.Web.Models.POS;

/// <summary>
/// Checkout form data
/// </summary>
public class CheckoutViewModel
{
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public int? CustomerID { get; set; }
    public int? StoreID { get; set; }
    public List<PaymentViewModel> Payments { get; set; } = new();
    public List<CartItemViewModel> Items { get; set; } = new();
    public int? PrescriptionID { get; set; }

    // Invoice-level discount
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }

    public decimal Total => Items.Sum(i => i.Subtotal);
}

/// <summary>
/// Payment information
/// </summary>
public class PaymentViewModel
{
    public string PaymentMethod { get; set; } = "Cash";
    public decimal Amount { get; set; }
    public string? ReferenceNumber { get; set; }
}
