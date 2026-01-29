using Microsoft.AspNetCore.Identity;
using PharmaCare.Application.Interfaces;
using PharmaCare.Domain.Entities.Security;

namespace PharmaCare.Web.Middleware;

/// <summary>
/// Middleware that ensures the session is initialized for authenticated users.
/// This handles the "Remember Me" scenario where the auth cookie persists but the session has expired.
/// </summary>
public class SessionInitializationMiddleware
{
    private readonly RequestDelegate _next;

    public SessionInitializationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ISessionService sessionService,
        UserManager<User> userManager)
    {
        // Skip for non-authenticated requests or static files
        if (!context.User.Identity?.IsAuthenticated ?? true)
        {
            await _next(context);
            return;
        }

        // Skip for login/logout/static file requests
        var path = context.Request.Path.Value?.ToLower() ?? "";
        if (path.Contains("/account/login") || 
            path.Contains("/account/logout") ||
            path.StartsWith("/lib/") ||
            path.StartsWith("/css/") ||
            path.StartsWith("/js/") ||
            path.StartsWith("/assets/"))
        {
            await _next(context);
            return;
        }

        // Check if session is already initialized
        var currentUser = sessionService.GetCurrentUser();
        if (currentUser == null)
        {
            // User is authenticated but session is empty - re-initialize it
            var user = await userManager.GetUserAsync(context.User);
            if (user != null)
            {
                await sessionService.InitializeSessionAsync(user.Id);
            }
        }

        await _next(context);
    }
}

/// <summary>
/// Extension method to add the middleware to the pipeline
/// </summary>
public static class SessionInitializationMiddlewareExtensions
{
    public static IApplicationBuilder UseSessionInitialization(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SessionInitializationMiddleware>();
    }
}
