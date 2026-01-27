using PharmaCare.Domain.Models.Base;
using PharmaCare.Domain.Models.Products;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PharmaCare.Domain.Models.PurchaseManagement;

public class PurchaseOrderItem
{
    [Key]
    public int PurchaseOrderItemID { get; set; }

    [ForeignKey("PurchaseOrder")]
    public int? PurchaseOrder_ID { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }

    [ForeignKey("Product")]
    public int? Product_ID { get; set; }
    public Product? Product { get; set; }

    [Required]
    public int Quantity { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal UnitPrice { get; set; } // Cost Price for this order

    [Column(TypeName = "decimal(18, 2)")]
    public decimal TotalPrice { get; set; }

    // For partial receiving tracking
    public int QuantityReceived { get; set; }
}
