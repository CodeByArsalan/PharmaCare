using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces;
using PharmaCare.Domain.Entities.Security;

namespace PharmaCare.Infrastructure.Implementations;

/// <summary>
/// Authentication service implementation
/// </summary>
public class AuthService : IAuthService
{
    private readonly SignInManager<User> _signInManager;
    private readonly UserManager<User> _userManager;
    private readonly PharmaCareDBContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthService(
        SignInManager<User> signInManager,
        UserManager<User> userManager,
        PharmaCareDBContext context,
        IHttpContextAccessor httpContextAccessor)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<AuthResult> LoginAsync(string email, string password, bool rememberMe)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            return new AuthResult { Success = false, ErrorMessage = "Invalid email or password" };
        }

        if (!user.IsActive)
        {
            return new AuthResult { Success = false, ErrorMessage = "Account is deactivated" };
        }

        var result = await _signInManager.PasswordSignInAsync(user, password, rememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            return new AuthResult { Success = true, User = user };
        }

        if (result.IsLockedOut)
        {
            return new AuthResult { Success = false, ErrorMessage = "Account is temporarily locked due to multiple failed login attempts. Please try again later." };
        }

        return new AuthResult { Success = false, ErrorMessage = "Invalid email or password" };
    }

    public async Task LogoutAsync()
    {
        await _signInManager.SignOutAsync();
    }

    public async Task<User?> GetCurrentUserAsync()
    {
        var userId = _httpContextAccessor.HttpContext?.User?.Identity?.Name;
        if (string.IsNullOrEmpty(userId))
            return null;

        return await _userManager.FindByNameAsync(userId);
    }

    public async Task<bool> HasPermissionAsync(int userId, string controller, string action, string permissionType)
    {
        // Shared resolution (PermissionResolution) rather than a private query. The private copy
        // this replaces had drifted from SessionService in three ways, each a real defect: it
        // consulted only the FIRST RolePage row across the user's roles instead of the union, it
        // ignored Role.IsActive, and it knew nothing about PageUrl aliases.
        var pages = await Security.PermissionResolution.EffectivePermissionsAsync(_context, userId);

        var page = pages.FirstOrDefault(p =>
            string.Equals(p.Controller, controller, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(p.Action, action, StringComparison.OrdinalIgnoreCase));

        if (page == null)
            return false;

        return permissionType.ToLower() switch
        {
            "view" => page.CanView,
            "create" => page.CanCreate,
            "edit" => page.CanEdit,
            "delete" => page.CanDelete,
            _ => false
        };
    }
}
