using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCare.Application.DTOs.Security;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Web.Filters;
using PharmaCare.Web.Utilities;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaCare.Application.Interfaces.Accounting;

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

    /// <summary>
    /// Shows a toast notification message after redirect.
    /// </summary>
    /// <param name="type">Type of message (Success, Error, Warning, Info)</param>
    /// <param name="message">The message to display</param>
    protected void ShowMessage(MessageType type, string message)
    {
        TempData["ToastType"] = type.ToString().ToLower();
        TempData["ToastMessage"] = message;
        ViewData["ToastType"] = type.ToString().ToLower();
        ViewData["ToastMessage"] = message;
    }

    /// <summary>
    /// Removes standard navigation properties from ModelState to avoid validation errors for entities.
    /// </summary>
    protected void CleanNavigationModelState(params string[] properties)
    {
        foreach (var prop in properties)
        {
            ModelState.Remove(prop);
        }
    }

    /// <summary>
    /// Validates a user-supplied page size against the allowed set, falling back to 25.
    /// Prevents arbitrary/huge page sizes from the query string.
    /// </summary>
    protected static int NormalizePageSize(int pageSize) =>
        pageSize is 10 or 25 or 50 or 100 ? pageSize : 25;

    /// <summary>
    /// Gets a SelectList for parties (Suppliers, Customers, or Both).
    /// </summary>
    protected async Task<SelectList> GetPartySelectListAsync(IPartyService partyService, string partyType, int? selectedId = null)
    {
        var parties = await partyService.GetAllAsync();
        var filtered = parties.Where(p => p.IsActive && (p.PartyType == partyType || p.PartyType == "Both"));
        return new SelectList(filtered, "PartyID", "Name", selectedId);
    }

    /// <summary>
    /// Common logic to fetch accounts based on payment method.
    /// </summary>
    protected async Task<IActionResult> GetAccountsByMethodAsync(IAccountService accountService, string method)
    {
        var accounts = await accountService.GetAccountsByMethodAsync(method);
        var result = accounts
            .Select(a => new { id = a.AccountID, name = a.Name })
            .ToList();

        return Json(result);
    }
}

/// <summary>
/// Message types for toast notifications.
/// </summary>
public enum MessageType
{
    Success,
    Error,
    Warning,
    Info
}
