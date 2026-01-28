using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PharmaCare.Domain.Entities.Base;

namespace PharmaCare.Domain.Entities.Accounting;

/// <summary>
/// Account Subhead - Level 2 of Chart of Accounts hierarchy.
/// Examples: Current Assets, Fixed Assets, Current Liabilities
/// </summary>
public class AccountSubhead : BaseEntityWithStatus
{
    [Key]
    public int AccountSubheadID { get; set; }

    [ForeignKey("AccountHead")]
    public int AccountHead_ID { get; set; }
    public AccountHead? AccountHead { get; set; }

    [Required]
    [StringLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    // Navigation
    public ICollection<Account> Accounts { get; set; } = new List<Account>();
}
