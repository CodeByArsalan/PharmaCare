namespace PharmaCare.Application.DTOs.Security;

/// <summary>
/// DTO for rendering sidebar menu items dynamically based on user permissions.
/// </summary>
public class SidebarMenuItemDTO
{
    public int PageId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Controller { get; set; }
    public string? Action { get; set; }
    public int? ParentId { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsVisible { get; set; }

    /// <summary>
    /// Child menu items for hierarchical rendering.
    /// </summary>
    public List<SidebarMenuItemDTO> Children { get; set; } = new();
}
