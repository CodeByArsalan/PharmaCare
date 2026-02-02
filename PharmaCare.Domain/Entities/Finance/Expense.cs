using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PharmaCare.Domain.Entities.Base;
using PharmaCare.Domain.Entities.Accounting;
using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.Domain.Entities.Finance;

/// <summary>
/// Expense transaction with automatic voucher creation.
/// </summary>
public class Expense : BaseEntity
{
    [Key]
    public int ExpenseID { get; set; }

    [ForeignKey("ExpenseCategory")]
    public int ExpenseCategory_ID { get; set; }
    public ExpenseCategory? ExpenseCategory { get; set; }


    /// <summary>
    /// Source account (Cash/Bank) - CREDIT side
    /// </summary>
    [ForeignKey("SourceAccount")]
    public int SourceAccount_ID { get; set; }
    public Account? SourceAccount { get; set; }

    /// <summary>
    /// Expense account - DEBIT side
    /// </summary>
    [ForeignKey("ExpenseAccount")]
    public int ExpenseAccount_ID { get; set; }
    public Account? ExpenseAccount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    public DateTime ExpenseDate { get; set; } = DateTime.Now;

    [StringLength(500)]
    public string? Description { get; set; }

    [StringLength(100)]
    public string? Reference { get; set; }

    [StringLength(200)]
    public string? VendorName { get; set; }

    /// <summary>
    /// Link to auto-generated accounting voucher
    /// </summary>
    [ForeignKey("Voucher")]
    public int? Voucher_ID { get; set; }
    public Voucher? Voucher { get; set; }
}
