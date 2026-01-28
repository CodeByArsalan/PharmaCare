using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PharmaCare.Domain.Entities.Security;

/// <summary>
/// Junction table for Role-Page permissions.
/// Defines CRUD permissions for each role on each page.
/// </summary>
public class RolePage
{
    [Key]
    public int RolePageID { get; set; }

    [ForeignKey("Role")]
    public int Role_ID { get; set; }
    public Role? Role { get; set; }

    [ForeignKey("Page")]
    public int Page_ID { get; set; }
    public Page? Page { get; set; }

    /// <summary>
    /// Permission to view/access the page
    /// </summary>
    public bool CanView { get; set; } = true;

    /// <summary>
    /// Permission to create new records
    /// </summary>
    public bool CanCreate { get; set; }

    /// <summary>
    /// Permission to edit existing records
    /// </summary>
    public bool CanEdit { get; set; }

    /// <summary>
    /// Permission to delete records
    /// </summary>
    public bool CanDelete { get; set; }
}
