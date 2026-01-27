using System.ComponentModel.DataAnnotations.Schema;
using PharmaCare.Domain.Models.Configuration;
using PharmaCare.Domain.Models.Products;

namespace PharmaCare.Domain.Models.SaleManagement;

/// <summary>
/// Represents a line item in a sale
/// </summary>
public class SaleLine
{
    public int SaleLineID { get; set; }
    public int? Sale_ID { get; set; }
    public int? Product_ID { get; set; }
    public int? ProductBatch_ID { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal Quantity { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal UnitPrice { get; set; }

    // ========== DISCOUNT ==========
    [Column(TypeName = "decimal(5, 2)")]
    public decimal DiscountPercent { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal DiscountAmount { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal NetAmount { get; set; }  // (Quantity * UnitPrice) - DiscountAmount

    // Navigation properties
    public Sale? Sale { get; set; }
    public Product? Product { get; set; }
    public ProductBatch? ProductBatch { get; set; }
}
