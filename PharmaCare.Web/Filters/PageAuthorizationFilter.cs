using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using PharmaCare.Application.Interfaces;

namespace PharmaCare.Web.Filters;

/// <summary>
/// Authorization filter that validates page-level access permissions for each request.
/// Uses cached session data for efficient permission checks without database queries.
/// </summary>
public class PageAuthorizationFilter : IAsyncAuthorizationFilter
{
    private readonly ISessionService _sessionService;

    public PageAuthorizationFilter(ISessionService sessionService)
    {
        _sessionService = sessionService;
    }

    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        // Skip authorization for endpoints marked with [AllowAnonymous]
        var endpoint = context.HttpContext.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<IAllowAnonymous>() != null)
        {
            return Task.CompletedTask;
        }

        // Get controller and action names
        var actionDescriptor = context.ActionDescriptor as ControllerActionDescriptor;
        if (actionDescriptor == null)
        {
            return Task.CompletedTask;
        }

        var controller = actionDescriptor.ControllerName;
        var action = actionDescriptor.ActionName;

        // Skip authorization for Account controller (login/logout/register)
        if (string.Equals(controller, "Account", StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        // Skip authorization for Home controller (dashboard is accessible to all authenticated users)
        if (string.Equals(controller, "Home", StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        // Check if user is authenticated
        var user = _sessionService.GetCurrentUser();
        if (user == null)
        {
            context.Result = new RedirectToActionResult("Login", "Account", null);
            return Task.CompletedTask;
        }

        // Check for [LinkedToPage] attribute — overrides the controller/action/permission resolution
        var linkedToPage = actionDescriptor.MethodInfo
            .GetCustomAttributes(typeof(LinkedToPageAttribute), false)
            .FirstOrDefault() as LinkedToPageAttribute;

        string resolvedController;
        string resolvedAction;
        string permissionType;

        if (linkedToPage != null)
        {
            // Use the linked page's controller/action and permission type
            resolvedController = linkedToPage.Controller;
            resolvedAction = linkedToPage.Action;
            permissionType = linkedToPage.PermissionType;
        }
        else
        {
            // Standard resolution: use current controller/action
            resolvedController = controller;
            resolvedAction = action;
            permissionType = DeterminePermissionType(context.HttpContext.Request.Method, action);
        }

        // Check page access
        if (!_sessionService.HasPageAccess(resolvedController, resolvedAction, permissionType))
        {
            context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
            return Task.CompletedTask;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Determines the permission type based on HTTP method and action name.
    /// </summary>
    private static string DeterminePermissionType(string httpMethod, string actionName)
    {
        var actionLower = actionName.ToLower();

        // Check action name patterns first
        if (actionLower.Contains("add") || actionLower.Contains("create"))
            return "create";

        if (actionLower.Contains("edit") || actionLower.Contains("update"))
            return "edit";

        if (actionLower.Contains("delete") || actionLower.Contains("toggle"))
            return "delete";

        // Fall back to HTTP method
        return httpMethod.ToUpper() switch
        {
            "POST" => "create",
            "PUT" or "PATCH" => "edit",
            "DELETE" => "delete",
            _ => "view"
        };
    }
}
