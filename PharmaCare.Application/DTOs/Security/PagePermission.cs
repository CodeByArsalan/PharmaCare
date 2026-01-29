namespace PharmaCare.Application.DTOs.Security;

/// <summary>
/// Serializable DTO for storing page permission in session.
/// Each entry represents a page the user can access with specific CRUD permissions.
/// </summary>
public class PagePermission
{
    public int PageId { get; set; }
    public string Controller { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public bool CanView { get; set; }
    public bool CanCreate { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
}
