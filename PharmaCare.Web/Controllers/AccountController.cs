using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Logging;
using PharmaCare.Application.Interfaces.Tenancy;
using PharmaCare.Domain.Entities.Security;
using PharmaCare.Domain.Enums;

namespace PharmaCare.Web.Controllers;

/// <summary>
/// Account controller for authentication
/// </summary>
public class AccountController : Controller
{
    private readonly SignInManager<User> _signInManager;
    private readonly UserManager<User> _userManager;
    private readonly ISessionService _sessionService;
    private readonly IActivityLogService _activityLogService;
    private readonly ICurrentTenant _currentTenant;
    private readonly IPharmacyService _pharmacyService;

    public AccountController(
        SignInManager<User> signInManager,
        UserManager<User> userManager,
        ISessionService sessionService,
        IActivityLogService activityLogService,
        ICurrentTenant currentTenant,
        IPharmacyService pharmacyService)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _sessionService = sessionService;
        _activityLogService = activityLogService;
        _currentTenant = currentTenant;
        _pharmacyService = pharmacyService;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        // If user is already authenticated, redirect to home
        if (User.Identity?.IsAuthenticated ?? false)
        {
            return RedirectToAction("Index", "Home");
        }
        
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (ModelState.IsValid)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user != null && user.IsActive)
            {
                // lockoutOnFailure: true makes Identity count failed attempts and lock the
                // account per the configured policy (5 attempts / 15 min).
                var result = await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: true);
                if (result.IsLockedOut)
                {
                    ModelState.AddModelError(string.Empty, "This account is temporarily locked due to multiple failed login attempts. Please try again later.");
                    return View(model);
                }
                if (result.Succeeded)
                {
                    // Resolve the tenant (pharmacy) for this user and guard suspended tenants.
                    if (!user.IsPlatformAdmin)
                    {
                        if (user.Pharmacy_ID is not int pharmacyId || pharmacyId <= 0)
                        {
                            await _signInManager.SignOutAsync();
                            ModelState.AddModelError(string.Empty, "Your account is not linked to a pharmacy. Please contact your administrator.");
                            return View(model);
                        }

                        if (!await _pharmacyService.IsOperationalAsync(pharmacyId))
                        {
                            await _signInManager.SignOutAsync();
                            ModelState.AddModelError(string.Empty, "Your pharmacy account is currently suspended. Please contact support.");
                            return View(model);
                        }

                        // Make the tenant available for the rest of THIS request so the
                        // tenant-filtered role/permission queries in InitializeSessionAsync resolve.
                        // Subsequent requests resolve the tenant from the auth-cookie claim.
                        _currentTenant.SetTenant(pharmacyId);
                    }

                    // Initialize session with user data and permissions
                    await _sessionService.InitializeSessionAsync(user.Id);

                    // Log login activity
                    await _activityLogService.LogActivityAsync(
                        user.Id,
                        user.UserName ?? user.Email ?? "Unknown",
                        ActivityType.Login,
                        "User",
                        user.Id.ToString(),
                        description: $"User '{user.UserName}' logged in");

                    // A bootstrap/temporary password must be replaced before anything else —
                    // the global MustChangePasswordFilter backs this up on every later request.
                    if (user.MustChangePassword)
                    {
                        TempData["Warning"] = "Your password was set for you and must be changed before you can continue.";
                        return RedirectToAction("ChangePassword");
                    }

                    // Platform super-admins land in the cross-pharmacy admin area.
                    if (user.IsPlatformAdmin)
                    {
                        return RedirectToAction("Index", "Pharmacies");
                    }

                    return RedirectToLocal(returnUrl);
                }
            }
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        // Log logout activity before clearing session
        var user = await _userManager.GetUserAsync(User);
        if (user != null)
        {
            await _activityLogService.LogActivityAsync(
                user.Id,
                user.UserName ?? user.Email ?? "Unknown",
                ActivityType.Logout,
                "User",
                user.Id.ToString(),
                description: $"User '{user.UserName}' logged out");
        }
        
        // Clear session before signing out
        _sessionService.ClearSession();
        await _signInManager.SignOutAsync();
        return RedirectToAction("Login");
    }

    // NOTE: there is deliberately no Register action. Public self-registration is disabled in the
    // multi-tenant model — pharmacies and their users are provisioned by the platform super-admin,
    // so a user is always created under a specific pharmacy, never tenant-less. The old actions
    // only redirected back to Login, and the login page no longer links to them.

    [HttpGet]
    public IActionResult ChangePassword()
    {
        return View(new ChangePasswordViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login");
        }

        var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (result.Succeeded)
        {
            TempData["Success"] = "Password changed successfully!";

            // A forced change is now satisfied: clear the flag and release the user into the app.
            if (user.MustChangePassword)
            {
                user.MustChangePassword = false;
                user.UpdatedAt = AppTime.Now;
                user.UpdatedBy = user.Id;
                await _userManager.UpdateAsync(user);

                return user.IsPlatformAdmin
                    ? RedirectToAction("Index", "Pharmacies")
                    : RedirectToAction("Index", "Home");
            }

            return RedirectToAction("ChangePassword");
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    public IActionResult AccessDenied()
    {
        return View();
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }
        return RedirectToAction("Index", "Home");
    }
}

/// <summary>
/// Login view model
/// </summary>
public class LoginViewModel
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
}

/// <summary>
/// Change password view model.
/// Deliberately carries NO password-strength rules: Identity's configured policy (see
/// Program.cs) is the single authority and its failures are surfaced via ModelState by the
/// controller. Duplicating length/complexity here would let the two drift apart.
/// The confirmation match is the one rule Identity cannot enforce — it never sees this field.
/// </summary>
public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Current password is required.")]
    [DataType(DataType.Password)]
    [Display(Name = "Current Password")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "New password is required.")]
    [DataType(DataType.Password)]
    [Display(Name = "New Password")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your new password.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm New Password")]
    [Compare(nameof(NewPassword), ErrorMessage = "The new password and confirmation do not match.")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}

