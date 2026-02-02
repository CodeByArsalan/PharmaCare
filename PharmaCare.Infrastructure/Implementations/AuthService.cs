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

        var result = await _signInManager.PasswordSignInAsync(user, password, rememberMe, lockoutOnFailure: false);
        if (result.Succeeded)
        {
            return new AuthResult { Success = true, User = user };
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

    public async Task<int?> GetCurrentUserStoreIdAsync()
    {
        // Store functionality removed - return null
        return await Task.FromResult<int?>(null);
    }

    public async Task<bool> HasPermissionAsync(int userId, string controller, string action, string permissionType)
    {
        // Get user's roles
        var userRoleIds = await _context.UserRoles_Custom
            .Where(ur => ur.User_ID == userId)
            .Select(ur => ur.Role_ID)
            .ToListAsync();

        if (!userRoleIds.Any())
            return false;

        // Get page by controller/action
        var page = await _context.Pages
            .FirstOrDefaultAsync(p => p.Controller == controller && p.Action == action);

        if (page == null)
            return false;

        // Check if any role has the required permission
        var rolePage = await _context.RolePages
            .Where(rp => userRoleIds.Contains(rp.Role_ID) && rp.Page_ID == page.PageID)
            .FirstOrDefaultAsync();

        if (rolePage == null)
            return false;

        return permissionType.ToLower() switch
        {
            "view" => rolePage.CanView,
            "create" => rolePage.CanCreate,
            "edit" => rolePage.CanEdit,
            "delete" => rolePage.CanDelete,
            _ => false
        };
    }
}
