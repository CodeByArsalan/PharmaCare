namespace PharmaCare.Domain.ViewModels;

/// <summary>
/// DTO for sidebar menu items with hierarchical structure
/// </summary>
public class MenuItemDto
{
    public int WebPageID { get; set; }
    public string? PageTitle { get; set; }
    public string? PageIcon { get; set; }
    public string? ControllerName { get; set; }
    public string? ViewName { get; set; }
    public int PageOrder { get; set; }
    public List<MenuItemDto> Children { get; set; } = new();
    public List<string> Urls { get; set; } = new();
}
