namespace PharmaCare.Web.Models.POS;

/// <summary>
/// Product search result with available batches
/// </summary>
public class ProductSearchResultViewModel
{
    public int ProductID { get; set; }  // Matches ProductID from database
    public string BrandName { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public List<BatchInfoViewModel> AvailableBatches { get; set; } = new();
}

/// <summary>
/// Batch information for a product
/// </summary>
public class BatchInfoViewModel
{
    public int ProductBatchID { get; set; }  // Matches ProductBatchID from database
    public string BatchNumber { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
    public decimal AvailableQuantity { get; set; }
    public decimal Price { get; set; }
    public bool IsExpiringSoon => (ExpiryDate - DateTime.Now).TotalDays <= 30;
}
