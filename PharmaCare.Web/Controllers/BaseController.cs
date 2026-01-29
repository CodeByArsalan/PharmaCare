using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCare.Application.DTOs.Security;
using PharmaCare.Application.Interfaces;
using PharmaCare.Web.Filters;

namespace PharmaCare.Web.Controllers;

/// <summary>
/// Base controller providing authentication, authorization, and user context.
/// All controllers requiring authentication should inherit from this class.
/// </summary>
[Authorize]
[ServiceFilter(typeof(PageAuthorizationFilter))]
public abstract class BaseController : Controller
{
    [FromServices]
    public ISessionService SessionService { get; set; } = null!;

    protected int CurrentUserId => SessionService.GetCurrentUser()?.UserId ?? 0;

    protected string CurrentUserName => SessionService.GetCurrentUser()?.FullName ?? "Unknown";

    protected string CurrentUserEmail => SessionService.GetCurrentUser()?.Email ?? string.Empty;

    protected int? CurrentStoreId => SessionService.GetCurrentUser()?.StoreId;

    protected string? CurrentStoreName => SessionService.GetCurrentUser()?.StoreName;

    protected List<int> CurrentUserRoleIds => SessionService.GetCurrentUser()?.RoleIds ?? new List<int>();

    protected List<string> CurrentUserRoleNames => SessionService.GetCurrentUser()?.RoleNames ?? new List<string>();

    /// <summary>
    /// Checks if the current user has a specific permission on a page.
    /// </summary>
    /// <param name="controller">Controller name</param>
    /// <param name="action">Action name</param>
    /// <param name="permissionType">Permission type: view, create, edit, delete</param>
    protected bool HasPermission(string controller, string action, string permissionType)
    {
        return SessionService.HasPageAccess(controller, action, permissionType);
    }

    /// <summary>
    /// Gets all pages the current user can access with their permissions.
    /// Useful for building navigation menus.
    /// </summary>
    protected List<PagePermission> GetAccessiblePages()
    {
        return SessionService.GetAccessiblePages();
    }

    /// <summary>
    /// Checks if the current user has any of the specified roles.
    /// </summary>
    /// <param name="roleNames">Role names to check</param>
    protected bool IsInRole(params string[] roleNames)
    {
        var userRoles = CurrentUserRoleNames;
        return roleNames.Any(r => userRoles.Contains(r, StringComparer.OrdinalIgnoreCase));
    }
}
