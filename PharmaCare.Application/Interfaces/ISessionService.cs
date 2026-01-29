using PharmaCare.Application.DTOs.Security;

namespace PharmaCare.Application.Interfaces;

/// <summary>
/// Service for managing user session data including authentication context and page permissions.
/// </summary>
public interface ISessionService
{
    /// <summary>
    /// Initialize session with user data and accessible pages after successful login.
    /// </summary>
    /// <param name="userId">The authenticated user's ID</param>
    Task InitializeSessionAsync(int userId);

    /// <summary>
    /// Clear all session data (logout).
    /// </summary>
    void ClearSession();

    /// <summary>
    /// Get the current user's session information.
    /// Returns null if no user is logged in.
    /// </summary>
    UserSessionInfo? GetCurrentUser();

    /// <summary>
    /// Get list of all pages the current user can access with their permissions.
    /// </summary>
    List<PagePermission> GetAccessiblePages();

    /// <summary>
    /// Check if the current user has a specific permission on a page.
    /// </summary>
    /// <param name="controller">Controller name</param>
    /// <param name="action">Action name</param>
    /// <param name="permissionType">Permission type: view, create, edit, delete</param>
    bool HasPageAccess(string controller, string action, string permissionType);

    /// <summary>
    /// Get the hierarchical sidebar menu for the current user.
    /// Only returns pages the user has view access to.
    /// </summary>
    List<SidebarMenuItemDTO> GetSidebarMenu();
}
