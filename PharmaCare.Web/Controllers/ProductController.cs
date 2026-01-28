using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PharmaCare.Domain.Entities.Configuration;
using PharmaCare.Infrastructure;

namespace PharmaCare.Web.Controllers;

/// <summary>
/// Product management controller
/// </summary>
[Authorize]
public class ProductController : Controller
{
    private readonly PharmaCareDBContext _context;

    public ProductController(PharmaCareDBContext context)
    {
        _context = context;
    }

    // GET: Product/Index
    public async Task<IActionResult> ProductsIndex()
    {
        var products = await _context.Products
            .Include(p => p.SubCategory)
                .ThenInclude(s => s!.Category)
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.Name)
            .ToListAsync();
        return View("ProductsIndex", products);
    }

    // GET: Product/AddProduct
    public async Task<IActionResult> AddProduct()
    {
        await LoadDropdowns();
        return View(new Product());
    }

    // POST: Product/AddProduct
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddProduct(Product product)
    {
        if (ModelState.IsValid)
        {
            // Auto-generate product code
            var lastProduct = await _context.Products
                .OrderByDescending(p => p.ProductID)
                .FirstOrDefaultAsync();
            int nextCode = (lastProduct?.ProductID ?? 0) + 1;
            product.Code = $"PRD{nextCode:D5}";
            
            product.CreatedAt = DateTime.Now;
            product.CreatedBy = 1;
            product.IsActive = true;
            product.IsDeleted = false;
            
            _context.Products.Add(product);
            await _context.SaveChangesAsync();
            
            TempData["Success"] = "Product created successfully!";
            return RedirectToAction("Index");
        }
        await LoadDropdowns();
        return View(product);
    }

    // GET: Product/EditProduct/5
    public async Task<IActionResult> EditProduct(int id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null || product.IsDeleted)
        {
            return NotFound();
        }
        await LoadDropdowns();
        return View(product);
    }

    // POST: Product/EditProduct/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditProduct(int id, Product product)
    {
        if (id != product.ProductID)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            var existing = await _context.Products.FindAsync(id);
            if (existing == null || existing.IsDeleted)
            {
                return NotFound();
            }

            existing.Name = product.Name;
            existing.SubCategory_ID = product.SubCategory_ID;
            existing.CostPrice = product.CostPrice;
            existing.SellingPrice = product.SellingPrice;
            existing.ReorderLevel = product.ReorderLevel;
            existing.IsActive = product.IsActive;
            existing.UpdatedAt = DateTime.Now;
            existing.UpdatedBy = 1;

            await _context.SaveChangesAsync();
            TempData["Success"] = "Product updated successfully!";
            return RedirectToAction("Index");
        }
        await LoadDropdowns();
        return View(product);
    }

    // POST: Product/Delete/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product != null)
        {
            product.IsDeleted = true;
            product.DeletedAt = DateTime.Now;
            product.DeletedBy = 1;
            await _context.SaveChangesAsync();
            TempData["Success"] = "Product deleted successfully!";
        }
        return RedirectToAction("Index");
    }

    private async Task LoadDropdowns()
    {
        var subCategories = await _context.SubCategories
            .Include(s => s.Category)
            .Where(s => !s.IsDeleted && s.IsActive)
            .OrderBy(s => s.Category!.Name)
            .ThenBy(s => s.Name)
            .Select(s => new { s.SubCategoryID, Display = s.Category!.Name + " > " + s.Name })
            .ToListAsync();
        ViewBag.SubCategories = new SelectList(subCategories, "SubCategoryID", "Display");
    }
}
