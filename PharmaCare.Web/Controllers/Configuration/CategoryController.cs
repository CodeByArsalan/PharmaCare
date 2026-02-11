using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaCare.Web.Utilities;
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
    public IActionResult AddCategory()
    {
        // await LoadAccountsDropdowns(); // Removed
        return View(new Category());
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCategory(Category category)
    {
        if (ModelState.IsValid)
        {
            await _categoryService.CreateAsync(category, CurrentUserId);
            ShowMessage(MessageType.Success, "Category created successfully!");
            return RedirectToAction("CategoriesIndex");
        }
        // await LoadAccountsDropdowns(); // Removed
        return View(category);
    }

    public async Task<IActionResult> EditCategory(string id)
    {
        int categoryId = Utility.DecryptId(id);
        if (categoryId == 0) return NotFound();
        var category = await _categoryService.GetByIdAsync(categoryId);
        if (category == null)
        {
            return NotFound();
        }
        // await LoadAccountsDropdowns(); // Removed
        return View(category);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCategory(string id, Category category)
    {
        int categoryId = Utility.DecryptId(id);
        if (categoryId != category.CategoryID) return NotFound();

        if (ModelState.IsValid)
        {
            var updated = await _categoryService.UpdateAsync(category, CurrentUserId);
            if (!updated)
            {
                return NotFound();
            }
            ShowMessage(MessageType.Success, "Category updated successfully!");
            return RedirectToAction("CategoriesIndex");
        }
        // await LoadAccountsDropdowns(); // Removed
        return View(category);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        int categoryId = Utility.DecryptId(id);
        if (categoryId == 0) return NotFound();

        await _categoryService.ToggleStatusAsync(categoryId, CurrentUserId);
        ShowMessage(MessageType.Success, "Category status updated successfully!");
        return RedirectToAction("CategoriesIndex");
    }
    // private async Task LoadAccountsDropdowns() { ... } // Removed
}
