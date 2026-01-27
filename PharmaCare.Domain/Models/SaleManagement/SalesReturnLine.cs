using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PharmaCare.Domain.Models.Products;
using PharmaCare.Domain.Models.Configuration;

namespace PharmaCare.Domain.Models.SaleManagement;

/// <summary>
/// Represents a line item in a sales return
/// </summary>
public class SalesReturnLine
{
    [Key]
    public int SalesReturnLineID { get; set; }

    [ForeignKey("SalesReturn")]
    public int SalesReturn_ID { get; set; }

    [ForeignKey("SaleLine")]
    public int? SaleLine_ID { get; set; }

    [ForeignKey("Product")]
    public int? Product_ID { get; set; }

    [ForeignKey("ProductBatch")]
    public int? ProductBatch_ID { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal Quantity { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal UnitPrice { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal Amount { get; set; }

    /// <summary>
    /// Whether to restock the returned items to inventory
    /// </summary>
    public bool RestockInventory { get; set; } = true;

    // Navigation properties
    public SalesReturn? SalesReturn { get; set; }
    public SaleLine? SaleLine { get; set; }
    public Product? Product { get; set; }
    public ProductBatch? ProductBatch { get; set; }
}
