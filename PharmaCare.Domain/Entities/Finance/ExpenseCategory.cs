using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PharmaCare.Domain.Entities.Base;
using PharmaCare.Domain.Entities.Accounting;

namespace PharmaCare.Domain.Entities.Finance;

/// <summary>
/// Expense Category with hierarchical structure.
/// </summary>
public class ExpenseCategory : BaseEntityWithStatus
{
    [Key]
    public int ExpenseCategoryID { get; set; }

    /// <summary>
    /// Parent category for hierarchical structure. Null for top-level categories.
    /// </summary>
    [ForeignKey("ParentCategory")]
    public int? Parent_ID { get; set; }
    public ExpenseCategory? ParentCategory { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Default expense account for this category
    /// </summary>
    [ForeignKey("DefaultExpenseAccount")]
    public int? DefaultExpenseAccount_ID { get; set; }
    public Account? DefaultExpenseAccount { get; set; }

    // Navigation
    public ICollection<ExpenseCategory> ChildCategories { get; set; } = new List<ExpenseCategory>();
    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
}
