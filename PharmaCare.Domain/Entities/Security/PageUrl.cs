using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PharmaCare.Domain.Entities.Security;

/// <summary>
/// Represents a URL (controller/action) associated with a page.
/// When a role has access to a Page, they also have access to all PageUrls linked to that Page.
/// </summary>
public class PageUrl
{
    [Key]
    public int PageUrlID { get; set; }

    /// <summary>
    /// The parent page this URL belongs to.
    /// </summary>
    [ForeignKey("Page")]
    public int Page_ID { get; set; }
    public Page Page { get; set; } = null!;

    /// <summary>
    /// Controller name (e.g., "User")
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Controller { get; set; } = string.Empty;

    /// <summary>
    /// Action name (e.g., "AddUser")
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Action { get; set; } = string.Empty;
}
