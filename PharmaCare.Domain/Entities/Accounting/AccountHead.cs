using System.ComponentModel.DataAnnotations;
using PharmaCare.Domain.Entities.Base;

namespace PharmaCare.Domain.Entities.Accounting;

/// <summary>
/// Account Head - Level 1 of Chart of Accounts hierarchy.
/// Examples: Assets, Liabilities, Equity, Revenue, Expenses
/// </summary>
public class AccountHead : BaseEntityWithStatus
{
    [Key]
    public int AccountHeadID { get; set; }

    /// <summary>
    /// Head code (1-5 typically)
    /// </summary>
    [Required]
    [StringLength(10)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Normal balance: "DEBIT" or "CREDIT"
    /// </summary>
    [Required]
    [StringLength(10)]
    public string NormalBalance { get; set; } = "DEBIT";

    public int DisplayOrder { get; set; }

    // Navigation
    public ICollection<AccountSubhead> AccountSubheads { get; set; } = new List<AccountSubhead>();
}
