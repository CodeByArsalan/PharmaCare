using Microsoft.AspNetCore.Mvc;
using PharmaCare.Application.Interfaces.Membership;
using PharmaCare.Domain.Models.Membership;
using PharmaCare.Domain.ViewModels;
using PharmaCare.Web.Controllers;

namespace PharmaCare.Web.Controllers.Membership;

public class UserTypeController(IUserTypeService _userTypeService) : BaseController
{
    public async Task<IActionResult> UserTypes()
    {
        var viewModel = new UserTypesViewModel
        {
            UserTypesList = await _userTypeService.GetAllAsync(),
            IsEditMode = false
        };
        return View(viewModel);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddUserType(UserTypesViewModel viewModel)
    {
        var userTypeModel = viewModel.CurrentUserType;

        userTypeModel.IsActive = true;
        userTypeModel.CreatedBy = LoginUserID;
        userTypeModel.CreatedDate = DateTime.Now;

        bool result = await _userTypeService.CreateAsync(userTypeModel);
        if (result)
        {
            ShowMessage(MessageBox.Success,"User Type Created Successfully");
        }
        else
        {
            ShowMessage(MessageBox.Error, "Failed to create User Type.");
        }
        return RedirectToAction(nameof(UserTypes));
    }
    public async Task<IActionResult> EditUserType(string id)
    {
        int decryptedId = DecryptId(id);
        var userType = await _userTypeService.GetByIdAsync(decryptedId);
        if (userType == null)
        {
            ShowMessage(MessageBox.Error, "User Type not found.");
            return RedirectToAction(nameof(UserTypes));
        }

        var viewModel = new UserTypesViewModel
        {
            CurrentUserType = userType,
            UserTypesList = await _userTypeService.GetAllAsync(),
            IsEditMode = true
        };
        return View("UserTypes", viewModel);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditUserType(UserTypesViewModel viewModel)
    {
        var userTypeModel = viewModel.CurrentUserType;

        userTypeModel.UpdatedBy = LoginUserID;
        userTypeModel.UpdatedDate = DateTime.Now;

        var existing = await _userTypeService.GetByIdAsync(userTypeModel.UserTypeID);
        if (existing != null)
        {
            userTypeModel.IsActive = existing.IsActive;
            userTypeModel.CreatedBy = existing.CreatedBy;
            userTypeModel.CreatedDate = existing.CreatedDate;
        }

        bool result = await _userTypeService.UpdateAsync(userTypeModel);
        if (result)
        {
            ShowMessage(MessageBox.Success, "User Type updated successfully!");
        }
        else
        {
            ShowMessage(MessageBox.Error, "Failed to update User Type.");
        }
        return RedirectToAction(nameof(UserTypes));
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUserType(string id)
    {
        int decryptedId = DecryptId(id);

        var userType = await _userTypeService.GetByIdAsync(decryptedId);
        if (userType != null)
        {
            userType.IsActive = !userType.IsActive;
            userType.UpdatedDate = DateTime.Now;
            userType.UpdatedBy = LoginUserID;

            bool result = await _userTypeService.UpdateAsync(userType);
            if (result)
            {
                string status = userType.IsActive ? "activated" : "deactivated";
                ShowMessage(MessageBox.Success, $"User Type {status} successfully!");
            }
            else
            {
                ShowMessage(MessageBox.Error, "Failed to update User Type status.");
            }
        }
        else
        {
            ShowMessage(MessageBox.Error, "User Type not found.");
        }

        return RedirectToAction(nameof(UserTypes));
    }
}
