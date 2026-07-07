using System.ComponentModel.DataAnnotations;
using PharmaCare.Domain.Entities.Base;

namespace PharmaCare.Domain.Entities.Tenancy;

/// <summary>
/// A tenant: one pharmacy business. Owns an isolated set of users, chart of accounts,
/// products, parties, transactions and finance records. The Pharmacy row itself is NOT
/// tenant-owned (it is managed by the platform super-admin), so it does not implement
/// <see cref="ITenantEntity"/>.
/// </summary>
public class Pharmacy : BaseEntityWithStatus
{
    [Key]
    public int PharmacyID { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Short unique code for the pharmacy (e.g. "ACME").</summary>
    [Required]
    [StringLength(50)]
    public string Code { get; set; } = string.Empty;

    /// <summary>Active | Suspended. Suspended pharmacies cannot log in.</summary>
    [Required]
    [StringLength(20)]
    public string Status { get; set; } = "Active";

    [StringLength(20)]
    public string? Phone { get; set; }

    [StringLength(100)]
    public string? Email { get; set; }

    [StringLength(500)]
    public string? Address { get; set; }
}
