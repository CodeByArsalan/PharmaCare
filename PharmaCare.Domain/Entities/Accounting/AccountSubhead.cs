using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PharmaCare.Domain.Entities.Accounting;

/// <summary>
/// Level 3: Account Subhead
/// Examples: Current Assets, Fixed Assets, Current Liabilities
/// </summary>
public class AccountSubhead
{
    [Key]
    public int AccountSubheadID { get; set; }

    [Required]
    [StringLength(100)]
    public string SubheadName { get; set; } = string.Empty;

    [ForeignKey("AccountHead")]
    public int AccountHead_ID { get; set; }
    public AccountHead? AccountHead { get; set; }

    // Navigation
    public ICollection<Account> Accounts { get; set; } = new List<Account>();
}
