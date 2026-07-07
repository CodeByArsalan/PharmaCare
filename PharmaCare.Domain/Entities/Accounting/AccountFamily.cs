using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PharmaCare.Domain.Entities.Base;

namespace PharmaCare.Domain.Entities.Accounting;

/// <summary>
/// Level 1: Account Family
/// Top-level classification for chart of accounts.
/// </summary>
public class AccountFamily : ITenantEntity
{
    // Tenant (pharmacy) that owns this row. Auto-filtered and stamped by the DbContext.
    public int Pharmacy_ID { get; set; }

    [Key]
    public int AccountFamilyID { get; set; }

    [Required]
    [StringLength(100)]
    public string FamilyName { get; set; } = string.Empty;

    // Navigation
    public ICollection<AccountHead> AccountHeads { get; set; } = new List<AccountHead>();
}
