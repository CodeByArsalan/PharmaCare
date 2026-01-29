namespace PharmaCare.Application.DTOs.Security;

/// <summary>
/// Serializable DTO for storing user information in session.
/// Loaded once at login for efficient access throughout the session.
/// </summary>
public class UserSessionInfo
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int? StoreId { get; set; }
    public string? StoreName { get; set; }
    public List<int> RoleIds { get; set; } = new();
    public List<string> RoleNames { get; set; } = new();
}
