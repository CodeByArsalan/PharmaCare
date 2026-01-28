using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Domain.Entities.Configuration;

namespace PharmaCare.Web.Controllers.Configuration;

public class SubCategoryController : Controller
{
    private readonly ISubCategoryService _subCategoryService;

    public SubCategoryController(ISubCategoryService subCategoryService)
    {
        _subCategoryService = subCategoryService;
    }

    private int GetCurrentUserId() => 1;

    public async Task<IActionResult> SubCategoriesIndex()
    {
        var subCategories = await _subCategoryService.GetAllAsync();
        return View("SubCategoriesIndex", subCategories);
    }
    public async Task<IActionResult> AddSubCategory()
    {
        await LoadCategoriesDropdown();
        return View(new SubCategory());
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSubCategory(SubCategory subCategory)
    {
        if (ModelState.IsValid)
        {
            await _subCategoryService.CreateAsync(subCategory, GetCurrentUserId());
            TempData["Success"] = "SubCategory created successfully!";
            return RedirectToAction("SubCategoriesIndex");
        }
        await LoadCategoriesDropdown();
        return View(subCategory);
    }
    public async Task<IActionResult> EditSubCategory(int id)
    {
        var subCategory = await _subCategoryService.GetByIdAsync(id);
        if (subCategory == null)
        {
            return NotFound();
        }
        await LoadCategoriesDropdown();
        return View(subCategory);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditSubCategory(int id, SubCategory subCategory)
    {
        if (id != subCategory.SubCategoryID)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            var updated = await _subCategoryService.UpdateAsync(subCategory, GetCurrentUserId());
            if (!updated)
            {
                return NotFound();
            }
            TempData["Success"] = "SubCategory updated successfully!";
            return RedirectToAction("SubCategoriesIndex");
        }
        await LoadCategoriesDropdown();
        return View(subCategory);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _subCategoryService.ToggleStatusAsync(id, GetCurrentUserId());
        TempData["Success"] = "SubCategory status updated successfully!";
        return RedirectToAction("SubCategoriesIndex");
    }
    private async Task LoadCategoriesDropdown()
    {
        var categories = await _subCategoryService.GetCategoriesForDropdownAsync();
        ViewBag.Categories = new SelectList(categories, "CategoryID", "Name");
    }
}
