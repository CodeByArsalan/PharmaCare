namespace PharmaCare.Domain.ViewModels;

/// <summary>
/// DTO that contains user details along with their assigned menu pages
/// </summary>
public class UserWithPagesDto
{
    public int UserID { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public int UserTypeID { get; set; }
    public string UserType { get; set; } = string.Empty;
    public List<MenuItemDto> MenuItems { get; set; } = new();
}
