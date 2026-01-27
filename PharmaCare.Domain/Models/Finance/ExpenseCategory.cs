using PharmaCare.Domain.Models.Base;

namespace PharmaCare.Domain.Models.Finance;

public class ExpenseCategory : BaseModelWithStatus
{
    public int ExpenseCategoryID { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    // Self-referencing for hierarchy
    public int? ParentCategory_ID { get; set; }

    // Navigation Properties
    public virtual ExpenseCategory? ParentCategory { get; set; }
    public virtual ICollection<ExpenseCategory> ChildCategories { get; set; } = new List<ExpenseCategory>();
    public virtual ICollection<Expense> Expenses { get; set; } = new List<Expense>();
}
