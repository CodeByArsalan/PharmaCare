using System.ComponentModel.DataAnnotations;
using PharmaCare.Domain.Entities.Base;

namespace PharmaCare.Domain.Entities.Security;

/// <summary>
/// Role entity for grouping permissions.
/// Users can have multiple roles.
/// </summary>
public class Role : BaseEntityWithStatus
{
    [Key]
    public int RoleID { get; set; }

    [Required]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Description { get; set; }

    /// <summary>
    /// System roles cannot be deleted (e.g., Administrator)
    /// </summary>
    public bool IsSystemRole { get; set; }

    // Navigation
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<RolePage> RolePages { get; set; } = new List<RolePage>();
}
