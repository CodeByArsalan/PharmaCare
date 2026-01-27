using Microsoft.AspNetCore.Mvc;
using PharmaCare.Application.Interfaces.Membership;
using PharmaCare.Domain.Models.Membership;

namespace PharmaCare.Web.Controllers.Membership;

public class SystemUserController(ISystemUserService _systemUserServices) : BaseController
{
    public async Task<IActionResult> UserIndex()
    {
        try
        {
            var lists = await _systemUserServices.GetUsers();
            return View(lists);
        }
        catch (Exception ex)
        {
            ShowMessage(MessageBox.Error, ex.Message);
            return View(new List<SystemUser>());
        }
    }

    [HttpGet]
    public async Task<IActionResult> AddUser()
    {
        ViewBag.WebPages = await _systemUserServices.GetAllWebPagesAsync();
        return View(new SystemUser());
    }

    [HttpPost]
    public async Task<IActionResult> AddUser(SystemUser user, List<int> SelectedPages)
    {
        try
        {
            user.CreatedBy = LoginUserID;
            var result = await _systemUserServices.CreateUser(user, user.Password, SelectedPages ?? new List<int>());
            if (result.Succeeded)
            {
                ShowMessage(MessageBox.Success, "User created successfully.");
                return RedirectToAction(nameof(UserIndex));
            }
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
                ShowMessage(MessageBox.Error, error.Description);
            }
        }
        catch (Exception ex)
        {
            ShowMessage(MessageBox.Error, ex.Message);
        }
        ViewBag.WebPages = await _systemUserServices.GetAllWebPagesAsync();
        return View(user);
    }

    [HttpGet]
    public async Task<IActionResult> EditUser(string id)
    {
        int decryptedId = DecryptId(id);
        var user = await _systemUserServices.GetUserById(decryptedId);
        if (user == null)
        {
            return NotFound();
        }
        ViewBag.WebPages = await _systemUserServices.GetAllWebPagesAsync();
        ViewBag.AssignedPageIds = await _systemUserServices.GetUserAssignedPageIdsAsync(decryptedId);
        return View(user);
    }

    [HttpPost]
    public async Task<IActionResult> EditUser(SystemUser user, List<int> SelectedPages)
    {
        ModelState.Remove("Password");
        ModelState.Remove("ConfirmPassword");

        try
        {
            var result = await _systemUserServices.UpdateUser(user, SelectedPages ?? new List<int>());
            if (result.Succeeded)
            {
                ShowMessage(MessageBox.Success, "User updated successfully.");
                return RedirectToAction(nameof(UserIndex));
            }
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
                ShowMessage(MessageBox.Error, error.Description);
            }
        }
        catch (Exception ex)
        {
            ShowMessage(MessageBox.Error, ex.Message);
        }
        ViewBag.WebPages = await _systemUserServices.GetAllWebPagesAsync();
        ViewBag.AssignedPageIds = await _systemUserServices.GetUserAssignedPageIdsAsync(user.Id);
        return View(user);
    }

    [HttpPost]
    public async Task<IActionResult> DeleteUser(string id)
    {
        try
        {
            int decryptedId = DecryptId(id);
            await _systemUserServices.DeleteUser(decryptedId);
            ShowMessage(MessageBox.Success, "User deleted successfully.");
        }
        catch (Exception ex)
        {
            ShowMessage(MessageBox.Error, ex.Message);
        }
        return RedirectToAction(nameof(UserIndex));
    }
}
