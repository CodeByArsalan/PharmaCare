using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PharmaCare.Domain.Entities.Base;

namespace PharmaCare.Domain.Entities.Security;

/// <summary>
/// Junction table for User-Role many-to-many relationship.
/// A user can have multiple roles.
/// </summary>
public class UserRole : ITenantEntity
{
    // Tenant (pharmacy) that owns this row. Auto-filtered and stamped by the DbContext.
    public int Pharmacy_ID { get; set; }

    [Key]
    public int UserRoleID { get; set; }

    [ForeignKey("User")]
    public int User_ID { get; set; }
    public User? User { get; set; }

    [ForeignKey("Role")]
    public int Role_ID { get; set; }
    public Role? Role { get; set; }
}
