using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces;
using PharmaCare.Domain.Entities.Security;
using PharmaCare.Infrastructure;

namespace PharmaCare.Web.Middleware;

/// <summary>
/// Middleware that ensures the session's permission snapshot is present AND current.
///
/// <para>
/// Two jobs. First, the "Remember Me" case: the auth cookie persisted but the session expired, so
/// the snapshot is rebuilt. Second — revocation: the snapshot is a copy of the user's permissions
/// taken at sign-in, and the session slides on every request, so without a check here a permission
/// withdrawn by an administrator would never reach a user who stays signed in. Every permission
/// change rotates the user's <see cref="User.PermissionsStamp"/>; this middleware compares the
/// stamp inside the snapshot against the database value (one indexed scalar read) and rebuilds the
/// snapshot the moment they disagree.
/// </para>
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
        UserManager<User> userManager,
        PharmaCareDBContext db)
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
            path.StartsWith("/js/"))
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
        else
        {
            // The snapshot exists — but is it CURRENT? Every permission change (role permissions
            // edited, role toggled, user's roles rewritten) rotates the user's PermissionsStamp.
            // A stale stamp means an administrator changed what this user may do after the
            // snapshot was taken, and the snapshot must not answer another request.
            var storedStamp = await db.Users
                .AsNoTracking()
                .Where(u => u.Id == currentUser.UserId)
                .Select(u => u.PermissionsStamp)
                .FirstOrDefaultAsync();

            if (!string.Equals(storedStamp, currentUser.PermissionsStamp, StringComparison.Ordinal))
            {
                await sessionService.InitializeSessionAsync(currentUser.UserId);
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
