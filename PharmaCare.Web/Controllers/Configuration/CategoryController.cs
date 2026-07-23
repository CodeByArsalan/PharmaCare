using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaCare.Web.Utilities;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Domain.Entities.Configuration;

namespace PharmaCare.Web.Controllers.Configuration;

public class CategoryController : BaseController
{
    private readonly ICategoryService _categoryService;
    private readonly ILogger<CategoryController> _logger;

    public CategoryController(ICategoryService categoryService, ILogger<CategoryController> logger)
    {
        _categoryService = categoryService;
        _logger = logger;
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
    public async Task<IActionResult> AddCategory(
        [Bind("Name,SaleAccount_ID,StockAccount_ID,COGSAccount_ID,DamageAccount_ID")] Category category)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await _categoryService.CreateAsync(category, CurrentUserId);
                ShowMessage(MessageType.Success, "Category created successfully!");
                return RedirectToAction("CategoriesIndex");
            }
            catch (InvalidOperationException ex)
            {
                ShowMessage(MessageType.Error, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating category");
                ShowMessage(MessageType.Error, "An unexpected error occurred while saving the category.");
            }
        }
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
    public async Task<IActionResult> EditCategory(string id,
        [Bind("CategoryID,Name,SaleAccount_ID,StockAccount_ID,COGSAccount_ID,DamageAccount_ID,IsActive")] Category category)
    {
        int categoryId = Utility.DecryptId(id);
        if (categoryId != category.CategoryID) return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                var updated = await _categoryService.UpdateAsync(category, CurrentUserId);
                if (!updated)
                {
                    return NotFound();
                }
                ShowMessage(MessageType.Success, "Category updated successfully!");
                return RedirectToAction("CategoriesIndex");
            }
            catch (InvalidOperationException ex)
            {
                ShowMessage(MessageType.Error, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating category {CategoryId}", category.CategoryID);
                ShowMessage(MessageType.Error, "An unexpected error occurred while saving the category.");
            }
        }
        return View(category);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        int categoryId = Utility.DecryptId(id);
        if (categoryId == 0) return NotFound();

        try
        {
            await _categoryService.ToggleStatusAsync(categoryId, CurrentUserId);
            ShowMessage(MessageType.Success, "Category status updated successfully!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling category {CategoryId}", categoryId);
            ShowMessage(MessageType.Error, "Could not change the category's status. Please try again.");
        }
        return RedirectToAction("CategoriesIndex");
    }
    // private async Task LoadAccountsDropdowns() { ... } // Removed
}
