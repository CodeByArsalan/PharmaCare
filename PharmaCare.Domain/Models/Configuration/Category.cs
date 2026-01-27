using System.ComponentModel.DataAnnotations;
using PharmaCare.Domain.Models.AccountManagement;
using PharmaCare.Domain.Models.Base;

namespace PharmaCare.Domain.Models.Configuration;

/// <summary>
/// Represents a product category with links to accounting accounts
/// </summary>
public class Category : BaseModelWithStatus
{
    [Key]
    public int CategoryID { get; set; }



    [Required(ErrorMessage = "Category Name is required")]
    [Display(Name = "Category Name")]
    [StringLength(100)]
    public string CategoryName { get; set; } = string.Empty;

    // FK to ChartOfAccount - SaleAccount (AccountType_ID = 7)
    [Display(Name = "Sale Account")]
    public int? SaleAccount_ID { get; set; }

    // FK to ChartOfAccount - StockAccount (AccountType_ID = 5)
    [Display(Name = "Stock Account")]
    public int? StockAccount_ID { get; set; }

    // FK to ChartOfAccount - COGSAccount (AccountType_ID = 6)
    [Display(Name = "COGS Account")]
    public int? COGSAccount_ID { get; set; }

    // FK to ChartOfAccount - DamageExpenseStock (AccountType_ID = 8)
    [Display(Name = "Damage Expense Account")]
    public int? DamageExpenseAccount_ID { get; set; }

    // Navigation properties for account relationships
    public virtual ChartOfAccount? SaleAccount { get; set; }
    public virtual ChartOfAccount? StockAccount { get; set; }
    public virtual ChartOfAccount? COGSAccount { get; set; }
    public virtual ChartOfAccount? DamageExpenseAccount { get; set; }

    // Navigation property for subcategories
    public virtual ICollection<SubCategory> SubCategories { get; set; } = new List<SubCategory>();
}
