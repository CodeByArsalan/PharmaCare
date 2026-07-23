using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaCare.Web.Utilities;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Domain.Entities.Configuration;

namespace PharmaCare.Web.Controllers.Configuration;

public class SubCategoryController : BaseController
{
    private readonly ISubCategoryService _subCategoryService;
    private readonly ILogger<SubCategoryController> _logger;

    public SubCategoryController(ISubCategoryService subCategoryService, ILogger<SubCategoryController> logger)
    {
        _subCategoryService = subCategoryService;
        _logger = logger;
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
            try
            {
                await _subCategoryService.CreateAsync(subCategory, CurrentUserId);
                ShowMessage(MessageType.Success, "SubCategory created successfully!");
                return RedirectToAction("SubCategoriesIndex");
            }
            catch (InvalidOperationException ex)
            {
                ShowMessage(MessageType.Error, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating sub-category");
                ShowMessage(MessageType.Error, "An unexpected error occurred while saving the sub-category.");
            }
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
            try
            {
                var updated = await _subCategoryService.UpdateAsync(subCategory, CurrentUserId);
                if (!updated)
                {
                    return NotFound();
                }
                ShowMessage(MessageType.Success, "SubCategory updated successfully!");
                return RedirectToAction("SubCategoriesIndex");
            }
            catch (InvalidOperationException ex)
            {
                ShowMessage(MessageType.Error, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating sub-category {SubCategoryId}", subCategory.SubCategoryID);
                ShowMessage(MessageType.Error, "An unexpected error occurred while saving the sub-category.");
            }
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

        try
        {
            await _subCategoryService.ToggleStatusAsync(subCategoryId, CurrentUserId);
            ShowMessage(MessageType.Success, "SubCategory status updated successfully!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling sub-category {SubCategoryId}", subCategoryId);
            ShowMessage(MessageType.Error, "Could not change the sub-category's status. Please try again.");
        }
        return RedirectToAction("SubCategoriesIndex");
    }
    // Helper removed
}
