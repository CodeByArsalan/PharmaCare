using PharmaCare.Domain.Models.Base;
using PharmaCare.Domain.Models.Products;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PharmaCare.Domain.Models.PurchaseManagement;

public class GrnItem
{
    [Key]
    public int GrnItemID { get; set; }

    [ForeignKey("Grn")]
    public int? Grn_ID { get; set; }
    public Grn? Grn { get; set; }

    [ForeignKey("Product")]
    public int? Product_ID { get; set; }
    public Product? Product { get; set; }

    [Required]
    [Column(TypeName = "decimal(18, 2)")]
    public decimal QuantityReceived { get; set; }

    [Required]
    public string BatchNumber { get; set; } = string.Empty;

    public DateTime ExpiryDate { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal CostPrice { get; set; } // Actual cost at receipt

    [Column(TypeName = "decimal(18, 2)")]
    public decimal SellingPrice { get; set; } // Updated selling price if changed
}
