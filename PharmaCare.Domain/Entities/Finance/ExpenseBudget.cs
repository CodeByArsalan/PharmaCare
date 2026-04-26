using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PharmaCare.Domain.Entities.Base;

namespace PharmaCare.Domain.Entities.Finance;

/// <summary>
/// Monthly budget for an expense category.
/// </summary>
public class ExpenseBudget : BaseEntity
{
    [Key]
    public int ExpenseBudgetID { get; set; }

    [ForeignKey("ExpenseCategory")]
    public int ExpenseCategory_ID { get; set; }
    public ExpenseCategory? ExpenseCategory { get; set; }

    [Required]
    public int Year { get; set; }

    [Required]
    [Range(1, 12)]
    public int Month { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal BudgetAmount { get; set; }

    public string? Remarks { get; set; }
}
