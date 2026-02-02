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
