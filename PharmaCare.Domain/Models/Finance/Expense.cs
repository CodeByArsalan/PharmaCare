using PharmaCare.Domain.Models.Base;
using PharmaCare.Domain.Models.AccountManagement;
using PharmaCare.Domain.Models.Inventory;
using System.ComponentModel.DataAnnotations.Schema;

namespace PharmaCare.Domain.Models.Finance;

public class Expense : BaseModel
{
    public int ExpenseID { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime ExpenseDate { get; set; }
    public string? Reference { get; set; }
    public string? VendorName { get; set; }
    public string? ReceiptNumber { get; set; }

    // Foreign Keys
    public int ExpenseCategory_ID { get; set; }
    public int? SourceAccount_ID { get; set; }    // Payment account (Cash/Bank) - CREDIT
    public int? ExpenseAccount_ID { get; set; }   // Expense account (where money went) - DEBIT

    public int? Voucher_ID { get; set; }          // Link to Account Voucher (Replaces JournalEntry)

    // Navigation Properties
    public virtual ExpenseCategory? ExpenseCategory { get; set; }

    [ForeignKey("SourceAccount_ID")]
    public virtual ChartOfAccount? SourceAccount { get; set; }

    [ForeignKey("ExpenseAccount_ID")]
    public virtual ChartOfAccount? ExpenseAccount { get; set; }

    [ForeignKey("Voucher_ID")]
    public virtual AccountVoucher? AccountVoucher { get; set; }
}
