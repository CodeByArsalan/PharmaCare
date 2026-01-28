using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PharmaCare.Domain.Entities.Base;

namespace PharmaCare.Domain.Entities.Security;

/// <summary>
/// Page/Menu item entity with hierarchical structure.
/// Used for role-based page access control.
/// </summary>
public class Page : BaseEntityWithStatus
{
    [Key]
    public int PageID { get; set; }

    /// <summary>
    /// Parent page for hierarchical menu structure. Null for top-level pages.
    /// </summary>
    [ForeignKey("ParentPage")]
    public int? Parent_ID { get; set; }
    public Page? ParentPage { get; set; }

    [Required]
    [StringLength(100)]
    public string Title { get; set; } = string.Empty;

    [StringLength(50)]
    public string? Icon { get; set; }

    [StringLength(100)]
    public string? Controller { get; set; }

    [StringLength(100)]
    public string? Action { get; set; }

    /// <summary>
    /// Display order within parent group
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Whether to show in navigation menu
    /// </summary>
    public bool IsVisible { get; set; } = true;

    // Navigation
    public ICollection<Page> ChildPages { get; set; } = new List<Page>();
    public ICollection<RolePage> RolePages { get; set; } = new List<RolePage>();
}
