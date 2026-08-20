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

        // Endpoints gated by a named authorization POLICY are not part of the tenant page-permission
        // system at all — the platform-admin area is the case in point. The policy has already been
        // evaluated by the authorization middleware, and these controllers deliberately have no rows
        // in the page catalogue, so running the page check here would deny them to everyone.
        if (endpoint?.Metadata.GetOrderedMetadata<IAuthorizeData>()
                .Any(a => !string.IsNullOrEmpty(a.Policy)) == true)
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

        var resolvedController = linkedToPage?.Controller ?? controller;
        var resolvedAction = linkedToPage?.Action ?? action;

        var permissionType = ResolvePermissionType(
            linkedToPage?.PermissionType, context.HttpContext.Request.Method, action);

        // A state-changing action whose required permission could not be established is REFUSED.
        // See ResolvePermissionType for why.
        if (permissionType == null)
        {
            context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
            return Task.CompletedTask;
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
    /// Establishes which permission an action requires, or <c>null</c> when that cannot be
    /// established and the request must therefore be refused.
    ///
    /// <para>
    /// An explicit <see cref="LinkedToPageAttribute.PermissionType"/> always wins. Otherwise the
    /// action NAME is consulted, which is reliable exactly when the name says what the action does
    /// to the data — <c>AddSale</c>, <c>EditPurchase</c>, <c>ToggleStatus</c>.
    /// </para>
    ///
    /// <para>
    /// It is NOT reliable for the rest, and that used to be the hole: names like <c>Void</c>,
    /// <c>Approve</c>, <c>Reverse</c>, <c>Open</c> and <c>SavePermissions</c> match no pattern, so a
    /// POST fell through to "create" and every one of those destructive endpoints was reachable by
    /// anyone holding nothing but create rights. Guessing "create" is the most permissive answer
    /// available, which is precisely backwards for an action we have failed to identify.
    /// </para>
    ///
    /// <para>
    /// So a state-changing request with no explicit declaration and no recognised name is refused.
    /// The cost is that a new mutating endpoint must say what it needs; the benefit is that
    /// forgetting to do so denies the request instead of silently granting it.
    /// </para>
    /// </summary>
    internal static string? ResolvePermissionType(string? declared, string httpMethod, string actionName)
    {
        if (!string.IsNullOrWhiteSpace(declared))
            return declared;

        var actionLower = actionName.ToLowerInvariant();

        if (actionLower.Contains("add") || actionLower.Contains("create"))
            return "create";

        if (actionLower.Contains("edit") || actionLower.Contains("update"))
            return "edit";

        if (actionLower.Contains("delete") || actionLower.Contains("toggle"))
            return "delete";

        // Nothing in the name. A read is safe to treat as a read; a write is not safe to guess.
        return IsStateChanging(httpMethod) ? null : "view";
    }

    internal static bool IsStateChanging(string httpMethod) =>
        httpMethod.ToUpperInvariant() is "POST" or "PUT" or "PATCH" or "DELETE";
}
