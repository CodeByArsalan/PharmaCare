using PharmaCare.Domain.Models.Base;
using PharmaCare.Domain.Models.Configuration;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PharmaCare.Domain.Models.PurchaseManagement;

public class PurchaseOrder : BaseModel
{
    [Key]
    public int PurchaseOrderID { get; set; }

    [Required]
    [Display(Name = "Purchase Order Number")]
    public string PurchaseOrderNumber { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Order Date")]
    public DateTime OrderDate { get; set; }

    [Display(Name = "Expected Delivery")]
    public DateTime? ExpectedDeliveryDate { get; set; }

    [Required]
    [ForeignKey("Party")]
    public int? Party_ID { get; set; }
    public Party? Party { get; set; }

    [Display(Name = "Status")]
    public string Status { get; set; } = "Pending"; // Pending, Received, Cancelled

    [Display(Name = "Total Amount")]
    public decimal TotalAmount { get; set; }

    public ICollection<PurchaseOrderItem> PurchaseOrderItems { get; set; } = new List<PurchaseOrderItem>();
}

