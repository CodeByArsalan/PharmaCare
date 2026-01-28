using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PharmaCare.Domain.Models.Base;
using PharmaCare.Domain.Models.Configuration;
using PharmaCare.Domain.Models.AccountManagement;
using PharmaCare.Domain.Models.Inventory;

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

    [ForeignKey("StockMain")]
    public int? StockMain_ID { get; set; } // Optional link to original purchase
    public StockMain? StockMain { get; set; }

    [Required]
    public string Status { get; set; } = "Pending"; // Pending, Approved, Completed, Cancelled

    [Column(TypeName = "decimal(18, 2)")]
    public decimal TotalAmount { get; set; }

    public string? Remarks { get; set; } = string.Empty;



    // Account Voucher (Replaces JournalEntry)
    [ForeignKey("AccountVoucher")]
    public int? Voucher_ID { get; set; }
    public AccountVoucher? AccountVoucher { get; set; }

    // Refund tracking - for when supplier payment was already made
    public string? RefundMethod { get; set; } // Cash, Bank, CreditNote, None
    public string? RefundStatus { get; set; } // Pending, Received, NotApplicable

    [Column(TypeName = "decimal(18, 2)")]
    public decimal RefundAmount { get; set; }



    // Refund Voucher (Replaces RefundJournalEntry)
    [ForeignKey("RefundAccountVoucher")]
    public int? RefundVoucher_ID { get; set; }
    public AccountVoucher? RefundAccountVoucher { get; set; }

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
