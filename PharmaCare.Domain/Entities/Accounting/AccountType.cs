using System.ComponentModel.DataAnnotations;

namespace PharmaCare.Domain.Entities.Accounting;

/// <summary>
/// Account Type - Classification of accounts.
/// Examples: Cash, Bank, Accounts Receivable, Accounts Payable, Inventory
/// </summary>
public class AccountType
{
    [Key]
    public int AccountTypeID { get; set; }

    [Required]
    [StringLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Description { get; set; }

    // Navigation
    public ICollection<Account> Accounts { get; set; } = new List<Account>();
}
