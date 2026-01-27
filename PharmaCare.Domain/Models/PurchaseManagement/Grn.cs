using PharmaCare.Domain.Models.Base;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PharmaCare.Domain.Models.Configuration;

namespace PharmaCare.Domain.Models.PurchaseManagement;

public class Grn : BaseModel
{
    [Key]
    public int GrnID { get; set; }

    [Required]
    [Display(Name = "GRN Number")]
    public string GrnNumber { get; set; } = string.Empty; // Auto-generated

    [ForeignKey("PurchaseOrder")]
    public int? PurchaseOrder_ID { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }

    [ForeignKey("Party")]
    public int? Party_ID { get; set; }
    public Party? Party { get; set; }

    [ForeignKey("Store")]
    public int Store_ID { get; set; }
    public Store? Store { get; set; }

    public int? JournalEntry_ID { get; set; } // Link to journal entry

    [Display(Name = "Invoice Number")]
    public string InvoiceNumber { get; set; } = string.Empty;

    // ========== PAYMENT TRACKING ==========

    /// <summary>
    /// Total amount of this GRN (sum of items)
    /// </summary>
    [Column(TypeName = "decimal(18, 2)")]
    [Display(Name = "Total Amount")]
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Amount paid so far
    /// </summary>
    [Column(TypeName = "decimal(18, 2)")]
    [Display(Name = "Amount Paid")]
    public decimal AmountPaid { get; set; }

    /// <summary>
    /// Remaining balance to be paid
    /// </summary>
    [Column(TypeName = "decimal(18, 2)")]
    [Display(Name = "Balance Amount")]
    public decimal BalanceAmount { get; set; }

    /// <summary>
    /// Payment status: Unpaid, Partial, Paid
    /// </summary>
    [StringLength(20)]
    [Display(Name = "Payment Status")]
    public string PaymentStatus { get; set; } = "Unpaid";

    /// <summary>
    /// Total amount of goods returned to supplier (cumulative)
    /// </summary>
    [Column(TypeName = "decimal(18, 2)")]
    [Display(Name = "Returned Amount")]
    public decimal ReturnedAmount { get; set; }

    public ICollection<GrnItem> GrnItems { get; set; } = new List<GrnItem>();
}

