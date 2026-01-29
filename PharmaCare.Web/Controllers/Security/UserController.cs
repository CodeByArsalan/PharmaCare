using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaCare.Application.DTOs.Security;
using PharmaCare.Application.Interfaces.Security;
using PharmaCare.Domain.Entities.Security;

namespace PharmaCare.Web.Controllers.Security;

/// <summary>
/// Controller for user management.
/// </summary>
public class UserController : BaseController
{
    private readonly IUserService _userService;

    public UserController(IUserService userService)
    {
        _userService = userService;
    }

    public async Task<IActionResult> UsersIndex()
    {
        var users = await _userService.GetAllUsersAsync();
        return View(users);
    }

    public async Task<IActionResult> AddUser()
    {
        await LoadDropdowns();
        return View(new UserViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddUser(UserViewModel model)
    {
        // Password is required for new users
        if (string.IsNullOrEmpty(model.Password))
        {
            ModelState.AddModelError("Password", "Password is required.");
        }

        if (!ModelState.IsValid)
        {
            await LoadDropdowns();
            return View(model);
        }

        var user = new User
        {
            FullName = model.FullName,
            Email = model.Email,
            PhoneNumber = model.PhoneNumber,
            Store_ID = model.Store_ID
        };

        var result = await _userService.CreateUserAsync(user, model.Password!, model.SelectedRoleIds, CurrentUserId);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "An error occurred.");
            await LoadDropdowns();
            return View(model);
        }

        TempData["Success"] = "User created successfully!";
        return RedirectToAction("UsersIndex");
    }

    public async Task<IActionResult> EditUser(int id)
    {
        var user = await _userService.GetUserByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        var roleIds = await _userService.GetUserRoleIdsAsync(id);

        var model = new UserViewModel
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber,
            Store_ID = user.Store_ID,
            SelectedRoleIds = roleIds,
            IsActive = user.IsActive
        };

        await LoadDropdowns();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditUser(int id, UserViewModel model)
    {
        if (id != model.Id)
        {
            return NotFound();
        }

        // Remove password validation for edit if not provided
        if (string.IsNullOrEmpty(model.Password))
        {
            ModelState.Remove("Password");
            ModelState.Remove("ConfirmPassword");
        }

        if (!ModelState.IsValid)
        {
            await LoadDropdowns();
            return View(model);
        }

        var user = new User
        {
            Id = model.Id,
            FullName = model.FullName,
            Email = model.Email,
            PhoneNumber = model.PhoneNumber,
            Store_ID = model.Store_ID
        };

        var result = await _userService.UpdateUserAsync(user, model.Password, model.SelectedRoleIds, CurrentUserId);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "An error occurred.");
            await LoadDropdowns();
            return View(model);
        }

        TempData["Success"] = "User updated successfully!";
        return RedirectToAction("UsersIndex");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        await _userService.ToggleUserStatusAsync(id, CurrentUserId);
        TempData["Success"] = "User status updated successfully!";
        return RedirectToAction("UsersIndex");
    }

    private async Task LoadDropdowns()
    {
        var roles = await _userService.GetRolesForDropdownAsync();
        var stores = await _userService.GetStoresForDropdownAsync();

        ViewBag.Roles = roles;
        ViewBag.Stores = new SelectList(stores, "StoreID", "Name");
    }
}
