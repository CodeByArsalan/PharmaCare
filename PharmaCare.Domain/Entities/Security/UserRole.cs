using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PharmaCare.Domain.Entities.Security;

/// <summary>
/// Junction table for User-Role many-to-many relationship.
/// A user can have multiple roles.
/// </summary>
public class UserRole
{
    [Key]
    public int UserRoleID { get; set; }

    [ForeignKey("User")]
    public int User_ID { get; set; }
    public User? User { get; set; }

    [ForeignKey("Role")]
    public int Role_ID { get; set; }
    public Role? Role { get; set; }
}
