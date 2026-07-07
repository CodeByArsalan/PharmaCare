using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PharmaCare.Domain.Entities.Base;

namespace PharmaCare.Domain.Entities.Accounting;

/// <summary>
/// Level 2: Account Head
/// Examples: Assets, Liabilities, Equity, Revenue, Expenses
/// </summary>
public class AccountHead : ITenantEntity
{
    // Tenant (pharmacy) that owns this row. Auto-filtered and stamped by the DbContext.
    public int Pharmacy_ID { get; set; }

    [Key]
    public int AccountHeadID { get; set; }

    [Required]
    [StringLength(100)]
    public string HeadName { get; set; } = string.Empty;

    /// <summary>
    /// Stable well-known code used to resolve this head within a tenant regardless of its
    /// auto-generated ID (e.g. "AR_HEAD", "AP_HEAD"). Seeded per pharmacy at provisioning.
    /// </summary>
    [StringLength(30)]
    public string? Code { get; set; }

    [ForeignKey("AccountFamily")]
    public int AccountFamily_ID { get; set; }
    public AccountFamily? AccountFamily { get; set; }

    // Navigation
    public ICollection<AccountSubhead> AccountSubheads { get; set; } = new List<AccountSubhead>();
    public ICollection<Account> Accounts { get; set; } = new List<Account>();
}
