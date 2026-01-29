using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PharmaCare.Domain.Entities.Base;

namespace PharmaCare.Domain.Entities.Accounting;

/// <summary>
/// Level 4: Chart of Accounts (Individual Accounts)
/// The actual transactional ledger accounts.
/// </summary>
public class Account : BaseEntityWithStatus
{
    [Key]
    public int AccountID { get; set; }

    [Required]
    [StringLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [ForeignKey("AccountSubhead")]
    public int AccountSubhead_ID { get; set; }
    public AccountSubhead? AccountSubhead { get; set; }

    [ForeignKey("AccountType")]
    public int AccountType_ID { get; set; }
    public AccountType? AccountType { get; set; }

    public bool IsSystemAccount { get; set; }
}
