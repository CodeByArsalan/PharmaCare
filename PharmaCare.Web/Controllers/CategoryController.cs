using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PharmaCare.Domain.Entities.Configuration;
using PharmaCare.Infrastructure;

namespace PharmaCare.Web.Controllers;

/// <summary>
/// Category management controller
/// </summary>
[Authorize]
public class CategoryController : Controller
{
    private readonly PharmaCareDBContext _context;

    public CategoryController(PharmaCareDBContext context)
    {
        _context = context;
    }

    // GET: Category/Index
    public async Task<IActionResult> CategoriesIndex()
    {
        var categories = await _context.Categories
            .Include(c => c.SaleAccount)
            .Include(c => c.StockAccount)
            .Include(c => c.COGSAccount)
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.Name)
            .ToListAsync();
        return View("CategoriesIndex", categories);
    }

    // GET: Category/AddCategory
    public async Task<IActionResult> AddCategory()
    {
        await LoadAccountsDropdowns();
        return View(new Category());
    }

    // POST: Category/AddCategory
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCategory(Category category)
    {
        if (ModelState.IsValid)
        {
            category.CreatedAt = DateTime.Now;
            category.CreatedBy = 1;
            category.IsActive = true;
            category.IsDeleted = false;
            
            _context.Categories.Add(category);
            await _context.SaveChangesAsync();
            
            TempData["Success"] = "Category created successfully!";
            return RedirectToAction("Index");
        }
        await LoadAccountsDropdowns();
        return View(category);
    }

    // GET: Category/EditCategory/5
    public async Task<IActionResult> EditCategory(int id)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category == null || category.IsDeleted)
        {
            return NotFound();
        }
        await LoadAccountsDropdowns();
        return View(category);
    }

    // POST: Category/EditCategory/5
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
            var existing = await _context.Categories.FindAsync(id);
            if (existing == null || existing.IsDeleted)
            {
                return NotFound();
            }

            existing.Name = category.Name;
            existing.SaleAccount_ID = category.SaleAccount_ID;
            existing.StockAccount_ID = category.StockAccount_ID;
            existing.COGSAccount_ID = category.COGSAccount_ID;
            existing.IsActive = category.IsActive;
            existing.UpdatedAt = DateTime.Now;
            existing.UpdatedBy = 1;

            await _context.SaveChangesAsync();
            TempData["Success"] = "Category updated successfully!";
            return RedirectToAction("Index");
        }
        await LoadAccountsDropdowns();
        return View(category);
    }

    // POST: Category/Delete/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category != null)
        {
            category.IsDeleted = true;
            category.DeletedAt = DateTime.Now;
            category.DeletedBy = 1;
            await _context.SaveChangesAsync();
            TempData["Success"] = "Category deleted successfully!";
        }
        return RedirectToAction("Index");
    }

    private async Task LoadAccountsDropdowns()
    {
        var accounts = await _context.Accounts
            .Where(a => !a.IsDeleted && a.IsActive)
            .OrderBy(a => a.Code)
            .Select(a => new { a.AccountID, Display = a.Code + " - " + a.Name })
            .ToListAsync();
        
        ViewBag.Accounts = new SelectList(accounts, "AccountID", "Display");
    }
}
