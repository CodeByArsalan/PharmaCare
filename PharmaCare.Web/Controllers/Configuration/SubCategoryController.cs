using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaCare.Web.Utilities;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Domain.Entities.Configuration;

namespace PharmaCare.Web.Controllers.Configuration;

public class SubCategoryController : BaseController
{
    private readonly ISubCategoryService _subCategoryService;

    public SubCategoryController(ISubCategoryService subCategoryService)
    {
        _subCategoryService = subCategoryService;
    }

    public async Task<IActionResult> SubCategoriesIndex()
    {
        var subCategories = await _subCategoryService.GetAllAsync();
        return View("SubCategoriesIndex", subCategories);
    }
    public IActionResult AddSubCategory()
    {
        // await LoadCategoriesDropdown(); // Removed
        return View(new SubCategory());
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSubCategory(SubCategory subCategory)
    {
        if (ModelState.IsValid)
        {
            await _subCategoryService.CreateAsync(subCategory, CurrentUserId);
            ShowMessage(MessageType.Success, "SubCategory created successfully!");
            return RedirectToAction("SubCategoriesIndex");
        }
        // await LoadCategoriesDropdown(); // Removed
        return View(subCategory);
    }
    public async Task<IActionResult> EditSubCategory(string id)
    {
        int subCategoryId = Utility.DecryptId(id);
        if (subCategoryId == 0) return NotFound();
        var subCategory = await _subCategoryService.GetByIdAsync(subCategoryId);
        if (subCategory == null)
        {
            return NotFound();
        }
        // await LoadCategoriesDropdown(); // Removed
        return View(subCategory);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditSubCategory(string id, SubCategory subCategory)
    {
        int subCategoryId = Utility.DecryptId(id);
        if (subCategoryId != subCategory.SubCategoryID) return NotFound();

        if (ModelState.IsValid)
        {
            var updated = await _subCategoryService.UpdateAsync(subCategory, CurrentUserId);
            if (!updated)
            {
                return NotFound();
            }
            ShowMessage(MessageType.Success, "SubCategory updated successfully!");
            return RedirectToAction("SubCategoriesIndex");
        }
        // await LoadCategoriesDropdown(); // Removed
        return View(subCategory);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        int subCategoryId = Utility.DecryptId(id);
        if (subCategoryId == 0) return NotFound();

        await _subCategoryService.ToggleStatusAsync(subCategoryId, CurrentUserId);
        ShowMessage(MessageType.Success, "SubCategory status updated successfully!");
        return RedirectToAction("SubCategoriesIndex");
    }
    // Helper removed
}
