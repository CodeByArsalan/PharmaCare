using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PharmaCare.Domain.Models.Products;
using PharmaCare.Domain.Models.Configuration;

namespace PharmaCare.Domain.Models.SaleManagement;

/// <summary>
/// Line item in a held sale
/// </summary>
public class HeldSaleLine
{
    [Key]
    public int HeldSaleLineID { get; set; }

    [ForeignKey("HeldSale")]
    public int HeldSale_ID { get; set; }

    [ForeignKey("Product")]
    public int? Product_ID { get; set; }

    [ForeignKey("ProductBatch")]
    public int? ProductBatch_ID { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal Quantity { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal UnitPrice { get; set; }

    [Column(TypeName = "decimal(5, 2)")]
    public decimal DiscountPercent { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal DiscountAmount { get; set; }

    // Navigation properties
    public HeldSale? HeldSale { get; set; }
    public Product? Product { get; set; }
    public ProductBatch? ProductBatch { get; set; }
}
