using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Domain.Entities.Configuration;

namespace PharmaCare.Web.Controllers.Configuration;

public class CategoryController : BaseController
{
    private readonly ICategoryService _categoryService;

    public CategoryController(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }


    public async Task<IActionResult> CategoriesIndex()
    {
        var categories = await _categoryService.GetAllAsync();
        return View("CategoriesIndex", categories);
    }
    public async Task<IActionResult> AddCategory()
    {
        await LoadAccountsDropdowns();
        return View(new Category());
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCategory(Category category)
    {
        if (ModelState.IsValid)
        {
            await _categoryService.CreateAsync(category, CurrentUserId);
            TempData["Success"] = "Category created successfully!";
            return RedirectToAction("CategoriesIndex");
        }
        await LoadAccountsDropdowns();
        return View(category);
    }
    public async Task<IActionResult> EditCategory(int id)
    {
        var category = await _categoryService.GetByIdAsync(id);
        if (category == null)
        {
            return NotFound();
        }
        await LoadAccountsDropdowns();
        return View(category);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCategory(int id, Category category)
    {
        if (id != category.CategoryID)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            var updated = await _categoryService.UpdateAsync(category, CurrentUserId);
            if (!updated)
            {
                return NotFound();
            }
            TempData["Success"] = "Category updated successfully!";
            return RedirectToAction("CategoriesIndex");
        }
        await LoadAccountsDropdowns();
        return View(category);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _categoryService.ToggleStatusAsync(id, CurrentUserId);
        TempData["Success"] = "Category status updated successfully!";
        return RedirectToAction("CategoriesIndex");
    }
    private async Task LoadAccountsDropdowns()
    {
        var accounts = await _categoryService.GetAccountsForDropdownAsync();
        ViewBag.Accounts = new SelectList(
            accounts.Select(a => new { a.AccountID, Display = a.Code + " - " + a.Name }),
            "AccountID", "Display");
    }
}
