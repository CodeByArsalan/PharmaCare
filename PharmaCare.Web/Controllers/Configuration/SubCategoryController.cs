using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Domain.Models.Configuration;
using PharmaCare.Domain.ViewModels;
using PharmaCare.Infrastructure.Interfaces;

namespace PharmaCare.Web.Controllers.Configuration;

[Authorize]
public class SubCategoryController(ISubCategoryService _subCategoryService, IComboBoxRepository _comboBox) : BaseController
{
    public async Task<IActionResult> SubCategories()
    {
        var viewModel = new SubCategoriesViewModel
        {
            SubCategoryList = await _subCategoryService.GetAllSubCategories(),
            Categories = _comboBox.GetCategories(),
            IsEditMode = false
        };
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSubCategory(SubCategoriesViewModel viewModel)
    {
        var subCategory = viewModel.CurrentSubCategory;
        subCategory.CreatedBy = LoginUserID;

        if (ModelState.IsValid)
        {
            if (await _subCategoryService.CreateSubCategory(subCategory))
            {
                ShowMessage(MessageBox.Success, "Sub Category created successfully");
                return RedirectToAction(nameof(SubCategories));
            }
            else
            {
                ShowMessage(MessageBox.Error, "Failed to create Sub Category.");
            }
        }
        viewModel.Categories = _comboBox.GetCategories(subCategory.Category_ID);
        viewModel.SubCategoryList = await _subCategoryService.GetAllSubCategories();
        return View("SubCategories", viewModel);
    }

    public async Task<IActionResult> EditSubCategory(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        int decryptedId = DecryptId(id);
        var subCategory = await _subCategoryService.GetSubCategoryById(decryptedId);
        if (subCategory == null) return NotFound();

        var viewModel = new SubCategoriesViewModel
        {
            CurrentSubCategory = subCategory,
            SubCategoryList = await _subCategoryService.GetAllSubCategories(),
            Categories = _comboBox.GetCategories(subCategory.Category_ID),
            IsEditMode = true
        };
        return View("SubCategories", viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditSubCategory(SubCategoriesViewModel viewModel)
    {
        var subCategory = viewModel.CurrentSubCategory;
        subCategory.UpdatedBy = LoginUserID;

        if (ModelState.IsValid)
        {
            await _subCategoryService.UpdateSubCategory(subCategory);
            ShowMessage(MessageBox.Success, "Sub Category updated successfully");
            return RedirectToAction(nameof(SubCategories));
        }

        viewModel.Categories = _comboBox.GetCategories(subCategory.Category_ID);
        viewModel.SubCategoryList = await _subCategoryService.GetAllSubCategories();
        return View("SubCategories", viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSubCategory(string id)
    {
        int decryptedId = DecryptId(id);
        await _subCategoryService.DeleteSubCategory(decryptedId);
        ShowMessage(MessageBox.Warning, "Sub Category Status Updated successfully");
        return RedirectToAction(nameof(SubCategories));
    }
}
