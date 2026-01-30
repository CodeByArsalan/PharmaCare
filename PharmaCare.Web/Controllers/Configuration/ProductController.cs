using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Domain.Entities.Configuration;

namespace PharmaCare.Web.Controllers.Configuration;

public class ProductController : BaseController
{
    private readonly IProductService _productService;

    public ProductController(IProductService productService)
    {
        _productService = productService;
    }

    public async Task<IActionResult> ProductsIndex()
    {
        var products = await _productService.GetAllAsync();
        return View("ProductsIndex", products);
    }
    public async Task<IActionResult> AddProduct()
    {
        await LoadDropdowns();
        return View(new Product());
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddProduct(Product product)
    {
        if (ModelState.IsValid)
        {
            await _productService.CreateAsync(product, CurrentUserId);
            TempData["Success"] = "Product created successfully!";
            return RedirectToAction("ProductsIndex");
        }
        await LoadDropdowns();
        return View(product);
    }
    public async Task<IActionResult> EditProduct(int id)
    {
        var product = await _productService.GetByIdAsync(id);
        if (product == null)
        {
            return NotFound();
        }
        await LoadDropdowns();
        return View(product);
    }
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
            var updated = await _productService.UpdateAsync(product, CurrentUserId);
            if (!updated)
            {
                return NotFound();
            }
            TempData["Success"] = "Product updated successfully!";
            return RedirectToAction("ProductsIndex");
        }
        await LoadDropdowns();
        return View(product);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _productService.ToggleStatusAsync(id, CurrentUserId);
        TempData["Success"] = "Product status updated successfully!";
        return RedirectToAction("ProductsIndex");
    }
    private async Task LoadDropdowns()
    {
        var subCategories = await _productService.GetSubCategoriesForDropdownAsync();
        ViewBag.SubCategories = new SelectList(subCategories, "SubCategoryID", "Name");

        var categories = await _productService.GetCategoriesForDropdownAsync();
        ViewBag.Categories = new SelectList(categories, "CategoryID", "Name");
    }
}
