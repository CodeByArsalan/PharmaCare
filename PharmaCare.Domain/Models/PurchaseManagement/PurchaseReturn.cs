using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PharmaCare.Domain.Models.Base;
using PharmaCare.Domain.Models.Configuration;
using PharmaCare.Domain.Models.AccountManagement;

namespace PharmaCare.Domain.Models.PurchaseManagement;

public class PurchaseReturn : BaseModel
{
    [Key]
    public int PurchaseReturnID { get; set; }

    [Required]
    public DateTime ReturnDate { get; set; } = DateTime.Now;

    [ForeignKey("Party")]
    public int Party_ID { get; set; }
    public Party? Party { get; set; }

    [ForeignKey("Store")]
    public int Store_ID { get; set; }
    public Store? Store { get; set; }

    [ForeignKey("Grn")]
    public int? Grn_ID { get; set; } // Optional link to original GRN
    public Grn? Grn { get; set; }

    [Required]
    public string Status { get; set; } = "Pending"; // Pending, Approved, Completed, Cancelled

    [Column(TypeName = "decimal(18, 2)")]
    public decimal TotalAmount { get; set; }

    public string? Remarks { get; set; } = string.Empty;

    // Journal Entry for inventory adjustment (DR: Accounts Payable, CR: Inventory)
    [ForeignKey("JournalEntry")]
    public int? JournalEntry_ID { get; set; }
    public JournalEntry? JournalEntry { get; set; }

    // Refund tracking - for when supplier payment was already made
    public string? RefundMethod { get; set; } // Cash, Bank, CreditNote, None
    public string? RefundStatus { get; set; } // Pending, Received, NotApplicable

    [Column(TypeName = "decimal(18, 2)")]
    public decimal RefundAmount { get; set; }

    // Journal Entry for refund (DR: Cash/Bank, CR: Accounts Payable)
    [ForeignKey("RefundJournalEntry")]
    public int? RefundJournalEntry_ID { get; set; }
    public JournalEntry? RefundJournalEntry { get; set; }

    public List<PurchaseReturnItem> PurchaseReturnItems { get; set; } = new();
}

public class PurchaseReturnItem
{
    [Key]
    public int PurchaseReturnItemID { get; set; }

    [ForeignKey("PurchaseReturn")]
    public int PurchaseReturn_ID { get; set; }
    public PurchaseReturn? PurchaseReturn { get; set; }

    [ForeignKey("ProductBatch")]
    public int ProductBatch_ID { get; set; }
    public ProductBatch? ProductBatch { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal Quantity { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal UnitPrice { get; set; } // Cost Price at time of return

    [Column(TypeName = "decimal(18, 2)")]
    public decimal TotalLineAmount { get; set; } // Quantity * UnitPrice

    public string Reason { get; set; } = string.Empty;
}
