using Microsoft.AspNetCore.Mvc;
using PharmaCare.Application.DTOs.Security;
using PharmaCare.Application.Interfaces.Security;
using PharmaCare.Domain.Entities.Security;
using PharmaCare.Web.Filters;
using PharmaCare.Web.Utilities;

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
    public async Task<IActionResult> AddRole([Bind("Name,Description")] Role role)
    {
        if (!ModelState.IsValid)
        {
            return View(role);
        }

        await _roleService.CreateRoleAsync(role, CurrentUserId);
        ShowMessage(MessageType.Success, "Role created successfully!");
        return RedirectToAction("RolesIndex");
    }

    public async Task<IActionResult> EditRole(string id)
    {
        int roleId = Utility.DecryptId(id);
        if (roleId == 0) return NotFound();
        var role = await _roleService.GetRoleByIdAsync(roleId);
        if (role == null)
        {
            return NotFound();
        }
        return View(role);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditRole(string id, [Bind("RoleID,Name,Description")] Role role)
    {
        int roleId = Utility.DecryptId(id);
        if (roleId != role.RoleID) return NotFound();

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
    public async Task<IActionResult> ToggleStatus(string id)
    {
        int roleId = Utility.DecryptId(id);
        if (roleId == 0) return NotFound();

        var result = await _roleService.ToggleRoleStatusAsync(roleId, CurrentUserId);
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
    public async Task<IActionResult> Permissions(string id)
    {
        int roleId = Utility.DecryptId(id);
        if (roleId == 0) return NotFound();
        var role = await _roleService.GetRoleByIdAsync(roleId);
        if (role == null)
        {
            return NotFound();
        }

        var permissions = await _roleService.GetPermissionsForRoleAsync(roleId);

        ViewBag.Role = role;
        return View(permissions);
    }

    /// <summary>
    /// Save permissions for a role.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [LinkedToPage("Role", "RolesIndex", PermissionType = "edit")]
    public async Task<IActionResult> SavePermissions(int roleId, List<RolePagePermissionDTO> permissions)
    {
        // roleId arrives raw from the form. UpdatePermissionsAsync refuses a role this pharmacy
        // does not own, so report that outcome rather than always claiming success.
        try
        {
            var saved = await _roleService.UpdatePermissionsAsync(roleId, permissions);
            if (!saved)
            {
                ShowMessage(MessageType.Error, "Role not found.");
                return RedirectToAction("RolesIndex");
            }

            ShowMessage(MessageType.Success, "Permissions saved successfully!");
        }
        catch (Exception ex)
        {
            // The service refuses to edit the system Administrator role's permissions.
            ShowMessage(MessageType.Error, SafeErrorMessage(ex, "Save role permissions"));
        }
        return RedirectToAction("RolesIndex");
    }
}
