using PharmaCare.Domain.Models.Base;

namespace PharmaCare.Domain.Models.AccountManagement;

/// <summary>
/// Defines the standard accounting account types
/// </summary>
public class AccountType : BaseModelWithStatus
{
    public int AccountTypeID { get; set; }

    /// <summary>
    /// Type name: Asset, Liability, Equity, Revenue, Expense, COGS
    /// </summary>
    public string TypeName { get; set; } = string.Empty;

    // Navigation Properties
    public virtual ICollection<ChartOfAccount> ChartOfAccounts { get; set; } = new List<ChartOfAccount>();
}
