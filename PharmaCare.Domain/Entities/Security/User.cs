using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace PharmaCare.Domain.Entities.Security;

/// <summary>
/// System user entity. Inherits from IdentityUser for authentication.
/// </summary>
public class User : IdentityUser<int>
{
    [Required]
    [StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// The pharmacy (tenant) this user belongs to. NULL only for platform super-admins,
    /// who operate above individual pharmacies. Normal users always belong to exactly one.
    /// </summary>
    public int? Pharmacy_ID { get; set; }

    /// <summary>
    /// True for the cross-pharmacy platform administrator(s). Platform admins have a NULL
    /// Pharmacy_ID and use the platform-admin area rather than the tenant business UI.
    /// </summary>
    public bool IsPlatformAdmin { get; set; }

    /// <summary>
    /// Forces a password change at next login. Set when the account's password is known outside
    /// the user's head — e.g. the bootstrap platform admin created from configuration — so the
    /// written-down credential stops working the moment the account is actually used.
    /// </summary>
    public bool MustChangePassword { get; set; }

    // Audit Trail
    public DateTime CreatedAt { get; set; }
    public int CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedBy { get; set; }



    // Navigation
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

    // Not mapped - for registration/update
    [NotMapped]
    [DataType(DataType.Password)]
    public string? Password { get; set; }

    [NotMapped]
    [DataType(DataType.Password)]
    [Compare("Password", ErrorMessage = "Passwords do not match.")]
    public string? ConfirmPassword { get; set; }
}
