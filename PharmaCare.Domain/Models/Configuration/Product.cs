using PharmaCare.Domain.Models.Base;
using PharmaCare.Domain.Models.Configuration;
using System.ComponentModel.DataAnnotations;

namespace PharmaCare.Domain.Models.Products;

/// <summary>
/// Represents a product in the pharmacy inventory
/// </summary>
public class Product : BaseModelWithStatus
{
    public int ProductID { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public decimal OpeningPrice { get; set; }
    public decimal OpeningQuantity { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string ProductName { get; set; } = string.Empty;
    [Required]
    public int SubCategory_ID { get; set; }

    public int? ReorderLevel { get; set; } // For low stock alerts

    // Navigation properties
    public SubCategory? SubCategory { get; set; }
    public ICollection<ProductBatch> ProductBatches { get; set; } = new List<ProductBatch>();
}
