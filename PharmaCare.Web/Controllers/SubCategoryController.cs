using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PharmaCare.Domain.Entities.Configuration;
using PharmaCare.Infrastructure;

namespace PharmaCare.Web.Controllers;

/// <summary>
/// SubCategory management controller
/// </summary>
[Authorize]
public class SubCategoryController : Controller
{
    private readonly PharmaCareDBContext _context;

    public SubCategoryController(PharmaCareDBContext context)
    {
        _context = context;
    }

    // GET: SubCategory/Index
    public async Task<IActionResult> SubCategoriesIndex()
    {
        var subCategories = await _context.SubCategories
            .Include(s => s.Category)
            .Where(s => !s.IsDeleted)
            .OrderBy(s => s.Category!.Name)
            .ThenBy(s => s.Name)
            .ToListAsync();
        return View("SubCategoriesIndex", subCategories);
    }

    // GET: SubCategory/AddSubCategory
    public async Task<IActionResult> AddSubCategory()
    {
        await LoadCategoriesDropdown();
        return View(new SubCategory());
    }

    // POST: SubCategory/AddSubCategory
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSubCategory(SubCategory subCategory)
    {
        if (ModelState.IsValid)
        {
            subCategory.CreatedAt = DateTime.Now;
            subCategory.CreatedBy = 1;
            subCategory.IsActive = true;
            subCategory.IsDeleted = false;
            
            _context.SubCategories.Add(subCategory);
            await _context.SaveChangesAsync();
            
            TempData["Success"] = "SubCategory created successfully!";
            return RedirectToAction("Index");
        }
        await LoadCategoriesDropdown();
        return View(subCategory);
    }

    // GET: SubCategory/EditSubCategory/5
    public async Task<IActionResult> EditSubCategory(int id)
    {
        var subCategory = await _context.SubCategories.FindAsync(id);
        if (subCategory == null || subCategory.IsDeleted)
        {
            return NotFound();
        }
        await LoadCategoriesDropdown();
        return View(subCategory);
    }

    // POST: SubCategory/EditSubCategory/5
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
            var existing = await _context.SubCategories.FindAsync(id);
            if (existing == null || existing.IsDeleted)
            {
                return NotFound();
            }

            existing.Name = subCategory.Name;
            existing.Category_ID = subCategory.Category_ID;
            existing.IsActive = subCategory.IsActive;
            existing.UpdatedAt = DateTime.Now;
            existing.UpdatedBy = 1;

            await _context.SaveChangesAsync();
            TempData["Success"] = "SubCategory updated successfully!";
            return RedirectToAction("Index");
        }
        await LoadCategoriesDropdown();
        return View(subCategory);
    }

    // POST: SubCategory/Delete/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var subCategory = await _context.SubCategories.FindAsync(id);
        if (subCategory != null)
        {
            subCategory.IsDeleted = true;
            subCategory.DeletedAt = DateTime.Now;
            subCategory.DeletedBy = 1;
            await _context.SaveChangesAsync();
            TempData["Success"] = "SubCategory deleted successfully!";
        }
        return RedirectToAction("Index");
    }

    private async Task LoadCategoriesDropdown()
    {
        var categories = await _context.Categories
            .Where(c => !c.IsDeleted && c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync();
        ViewBag.Categories = new SelectList(categories, "CategoryID", "Name");
    }
}
