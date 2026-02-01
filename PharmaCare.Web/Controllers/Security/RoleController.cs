using Microsoft.AspNetCore.Mvc;
using PharmaCare.Application.DTOs.Security;
using PharmaCare.Application.Interfaces.Security;
using PharmaCare.Domain.Entities.Security;

namespace PharmaCare.Web.Controllers.Security;

/// <summary>
/// Controller for role and permission management.
/// </summary>
public class RoleController : BaseController
{
    private readonly IRoleService _roleService;

    public RoleController(IRoleService roleService)
    {
        _roleService = roleService;
    }

    public async Task<IActionResult> RolesIndex()
    {
        var roles = await _roleService.GetAllRolesAsync();
        return View(roles);
    }

    public IActionResult AddRole()
    {
        return View(new Role());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddRole(Role role)
    {
        if (!ModelState.IsValid)
        {
            return View(role);
        }

        await _roleService.CreateRoleAsync(role, CurrentUserId);
        ShowMessage(MessageType.Success, "Role created successfully!");
        return RedirectToAction("RolesIndex");
    }

    public async Task<IActionResult> EditRole(int id)
    {
        var role = await _roleService.GetRoleByIdAsync(id);
        if (role == null)
        {
            return NotFound();
        }
        return View(role);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditRole(int id, Role role)
    {
        if (id != role.RoleID)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(role);
        }

        var updated = await _roleService.UpdateRoleAsync(role, CurrentUserId);
        if (!updated)
        {
            return NotFound();
        }

        ShowMessage(MessageType.Success, "Role updated successfully!");
        return RedirectToAction("RolesIndex");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var result = await _roleService.ToggleRoleStatusAsync(id, CurrentUserId);
        if (!result)
        {
            ShowMessage(MessageType.Error, "Cannot modify system roles.");
        }
        else
        {
            ShowMessage(MessageType.Success, "Role status updated successfully!");
        }
        return RedirectToAction("RolesIndex");
    }

    /// <summary>
    /// Display permissions grid for a role.
    /// </summary>
    public async Task<IActionResult> Permissions(int id)
    {
        var role = await _roleService.GetRoleByIdAsync(id);
        if (role == null)
        {
            return NotFound();
        }

        var permissions = await _roleService.GetPermissionsForRoleAsync(id);

        ViewBag.Role = role;
        return View(permissions);
    }

    /// <summary>
    /// Save permissions for a role.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePermissions(int roleId, List<RolePagePermissionDTO> permissions)
    {
        await _roleService.UpdatePermissionsAsync(roleId, permissions);
        ShowMessage(MessageType.Success, "Permissions saved successfully!");
        return RedirectToAction("RolesIndex");
    }
}
