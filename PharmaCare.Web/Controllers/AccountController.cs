using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Membership;
using PharmaCare.Domain.Models.Membership;
using PharmaCare.Web.Models;

namespace PharmaCare.Web.Controllers;

[AllowAnonymous]
public class AccountController(UserManager<SystemUser> _userManager, ISystemUserService _user, IAuthService _authService) : Controller
{

    [HttpGet]
    public async Task<IActionResult> Login(string? returnUrl = null)
    {
        // Check if any users exist, if not redirect to registration
        if (!await _authService.AnyUsersExistAsync())
        {
            return RedirectToPage("/Account/Register", new { area = "Identity" });
        }

        // If already authenticated, check if the required claim exists
        if (User.Identity?.IsAuthenticated == true)
        {
            var loginUserClaim = User.Claims.FirstOrDefault(d => d.Type == "LoginUserDetail")?.Value;
            if (!string.IsNullOrEmpty(loginUserClaim))
            {
                // Valid login with claims - redirect to dashboard
                return RedirectToAction("Index", "Home");
            }
            else
            {
                // Authenticated but missing claims (corrupt state) - force logout
                await _authService.LogoutAsync();
            }
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    //[ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
        {
            // Log validation errors for debugging
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            Console.WriteLine($"Login validation failed: {string.Join(", ", errors)}");
            return View(model);
        }

        // Attempt to sign in
        var result = await _authService.LoginAsync(
            model.Email,
            model.Password,
            model.RememberMe
        );

        if (result.Succeeded)
        {
            var LoginUser = await _userManager.FindByEmailAsync(model.Email);

            // Guard against null - should rarely happen but prevents intermittent failures
            if (LoginUser != null)
            {
                try
                {
                    HttpContext.Session.SetString("AspNetUserPage", _user.GetUserPagesJson(LoginUser.Id));
                }
                catch (Exception ex)
                {
                    // Log the error but don't prevent login
                    Console.WriteLine($"Warning: Failed to load user pages for user {LoginUser.Id}: {ex.Message}");
                    // Continue with login even if menu loading fails
                }
            }

            // Redirect to return URL or dashboard
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Index", "Home");
        }

        if (result.RequiresTwoFactor)
        {
            ModelState.AddModelError(string.Empty, "Two-factor authentication required.");
            return View(model);
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "Account locked out. Please try again later.");
            return View(model);
        }

        // Login failed
        ModelState.AddModelError(string.Empty, "Invalid email or password.");
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _authService.LogoutAsync();
        return RedirectToAction("Login", "Account");
    }

    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (ModelState.IsValid)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null || !(await _userManager.IsEmailConfirmedAsync(user)))
            {
                // Don't reveal that the user does not exist or is not confirmed
                return RedirectToAction(nameof(ForgotPasswordConfirmation));
            }

            // For more info on how to enable account confirmation and password reset please 
            // visit https://go.microsoft.com/fwlink/?LinkID=532713
            var code = await _userManager.GeneratePasswordResetTokenAsync(user);

            // TODO: Send email with this code
            // For now, redirect to ResetPassword with the code
            return RedirectToAction(nameof(ResetPassword), new { code, email = model.Email });
        }

        return View(model);
    }

    [HttpGet]
    public IActionResult ForgotPasswordConfirmation()
    {
        return View();
    }

    [HttpGet]
    public IActionResult ResetPassword(string? code = null, string? email = null)
    {
        if (code == null)
        {
            return BadRequest("A code must be supplied for password reset.");
        }
        var model = new ResetPasswordViewModel { Code = code, Email = email };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }
        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            // Don't reveal that the user does not exist
            return RedirectToAction(nameof(ResetPasswordConfirmation));
        }
        var result = await _userManager.ResetPasswordAsync(user, model.Code, model.Password);
        if (result.Succeeded)
        {
            return RedirectToAction(nameof(ResetPasswordConfirmation));
        }
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }
        return View();
    }

    [HttpGet]
    public IActionResult ResetPasswordConfirmation()
    {
        return View();
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Profile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
        }

        // Fetch full user details including UserType from ISystemUserService
        var systemUser = await _user.GetUserById(user.Id);

        var model = new UserProfileViewModel
        {
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            UserType = systemUser?.UserType?.UserType ?? "Unknown"
        };

        return View(model);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(UserProfileViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
        }

        if (model.FullName != user.FullName)
        {
            user.FullName = model.FullName;
        }
        if (model.PhoneNumber != user.PhoneNumber)
        {
            user.PhoneNumber = model.PhoneNumber;
        }

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        TempData["StatusMessage"] = "Your profile has been updated";
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    [Authorize]
    public IActionResult Settings()
    {
        return View();
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Settings(SettingsViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
        }

        var changePasswordResult = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!changePasswordResult.Succeeded)
        {
            foreach (var error in changePasswordResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        await _authService.LoginAsync(user.Email!, model.NewPassword, false);
        TempData["StatusMessage"] = "Your password has been changed.";
        return RedirectToAction("Settings");
    }
}
