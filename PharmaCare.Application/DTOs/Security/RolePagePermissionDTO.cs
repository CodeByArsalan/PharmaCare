namespace PharmaCare.Application.DTOs.Security;

/// <summary>
/// DTO for managing role-page permissions in the UI.
/// Represents a page and its permission state for a specific role.
/// </summary>
public class RolePagePermissionDTO
{
    public int PageId { get; set; }
    public string PageTitle { get; set; } = string.Empty;
    public int? ParentId { get; set; }
    public string? ParentTitle { get; set; }
    public string? Controller { get; set; }
    public string? Action { get; set; }
    public int DisplayOrder { get; set; }

    // Permission flags
    public bool CanView { get; set; }
    public bool CanCreate { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
}
