using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PharmaCare.Domain.Entities.Base;

namespace PharmaCare.Domain.Entities.Accounting;

/// <summary>
/// Level 3: Account Subhead
/// Examples: Current Assets, Fixed Assets, Current Liabilities
/// </summary>
public class AccountSubhead : ITenantEntity
{
    // Tenant (pharmacy) that owns this row. Auto-filtered and stamped by the DbContext.
    public int Pharmacy_ID { get; set; }

    [Key]
    public int AccountSubheadID { get; set; }

    [Required]
    [StringLength(100)]
    public string SubheadName { get; set; } = string.Empty;

    /// <summary>
    /// Stable well-known code used to resolve this subhead within a tenant regardless of its
    /// auto-generated ID (e.g. "AR_SUB", "AP_SUB"). Seeded per pharmacy at provisioning.
    /// </summary>
    [StringLength(30)]
    public string? Code { get; set; }

    [ForeignKey("AccountHead")]
    public int AccountHead_ID { get; set; }
    public AccountHead? AccountHead { get; set; }

    // Navigation
    public ICollection<Account> Accounts { get; set; } = new List<Account>();
}
