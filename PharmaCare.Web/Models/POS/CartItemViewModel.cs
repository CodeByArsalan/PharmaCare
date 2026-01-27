namespace PharmaCare.Web.Models.POS;

/// <summary>
/// Represents an item in the shopping cart
/// </summary>
public class CartItemViewModel
{
    public int ProductID { get; set; }  // Matches Product_ID from database
    public int ProductBatchID { get; set; }  // Matches ProductBatch_ID from database
    public string ProductName { get; set; } = string.Empty;

    public string BatchNumber { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal => Quantity * UnitPrice;
}
