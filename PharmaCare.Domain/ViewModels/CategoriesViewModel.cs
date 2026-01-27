using PharmaCare.Domain.Models.Configuration;

namespace PharmaCare.Domain.ViewModels;

public class CategoriesViewModel
{
    public Category CurrentCategory { get; set; } = new Category();
    public List<Category> CategoryList { get; set; } = new List<Category>();
    public bool IsEditMode { get; set; }

    public Microsoft.AspNetCore.Mvc.Rendering.SelectList? SaleAccounts { get; set; }
    public Microsoft.AspNetCore.Mvc.Rendering.SelectList? StockAccounts { get; set; }
    public Microsoft.AspNetCore.Mvc.Rendering.SelectList? COGSAccounts { get; set; }
    public Microsoft.AspNetCore.Mvc.Rendering.SelectList? DamageExpenseAccounts { get; set; }
}
