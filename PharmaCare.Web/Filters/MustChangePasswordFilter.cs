using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using PharmaCare.Domain.Entities.Security;

namespace PharmaCare.Web.Filters;

/// <summary>
/// While a signed-in user carries MustChangePassword, every page except the change-password
/// screen (and logout) redirects there. Enforcing it here rather than only at login means the
/// requirement cannot be skipped by typing a URL directly.
/// </summary>
public class MustChangePasswordFilter : IAsyncActionFilter
{
    private readonly UserManager<User> _userManager;

    public MustChangePasswordFilter(UserManager<User> userManager)
    {
        _userManager = userManager;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated == true
            && context.ActionDescriptor is ControllerActionDescriptor descriptor
            && !IsExemptAction(descriptor))
        {
            var user = await _userManager.GetUserAsync(context.HttpContext.User);
            if (user is { MustChangePassword: true })
            {
                context.Result = new RedirectToActionResult("ChangePassword", "Account", null);
                return;
            }
        }

        await next();
    }

    private static bool IsExemptAction(ControllerActionDescriptor descriptor)
    {
        if (!string.Equals(descriptor.ControllerName, "Account", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return descriptor.ActionName is "ChangePassword" or "Logout" or "AccessDenied" or "Login";
    }
}
