using PharmaCare.Domain.Entities.Security;

namespace PharmaCare.Application.Interfaces;

/// <summary>
/// Authentication service interface
/// </summary>
public interface IAuthService
{
    Task<AuthResult> LoginAsync(string email, string password, bool rememberMe);
    Task LogoutAsync();
    Task<User?> GetCurrentUserAsync();
    Task<int?> GetCurrentUserStoreIdAsync();
    Task<bool> HasPermissionAsync(int userId, string controller, string action, string permissionType);
}

/// <summary>
/// Authentication result
/// </summary>
public class AuthResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public User? User { get; set; }
}
