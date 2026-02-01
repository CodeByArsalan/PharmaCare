using Microsoft.AspNetCore.Mvc;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Domain.Entities.Configuration;
using PharmaCare.ViewModels;

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
        var priceTypes = await _productService.GetPriceTypesAsync();
        var vm = new ProductViewModel();
        
        foreach (var pt in priceTypes)
        {
            vm.ProductPrices.Add(new Application.DTOs.Configuration.ProductPriceDto
            {
                PriceTypeId = pt.PriceTypeID,
                PriceTypeName = pt.PriceTypeName,
                Price = 0,
                IsSelected = false
            });
        }
        
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddProduct(ProductViewModel vm)
    {
        if (ModelState.IsValid)
        {
            // Create Product
            var createdProduct = await _productService.CreateAsync(vm, CurrentUserId);
            
            // Save Prices
            await _productService.SaveProductPricesAsync(createdProduct.ProductID, vm.ProductPrices, CurrentUserId);
            
            ShowMessage(MessageType.Success, "Product created successfully!");
            return RedirectToAction("ProductsIndex");
        }
        
        // If failed, reload valid price types just in case (though model should hold them)
        // Usually needed if we want to ensure names are correct if they weren't bound or were manipulated
         if (!vm.ProductPrices.Any())
         {
             var priceTypes = await _productService.GetPriceTypesAsync();
             foreach (var pt in priceTypes)
             {
                 vm.ProductPrices.Add(new Application.DTOs.Configuration.ProductPriceDto
                 {
                     PriceTypeId = pt.PriceTypeID,
                     PriceTypeName = pt.PriceTypeName
                 });
             }
         }
        
        return View(vm);
    }

    public async Task<IActionResult> EditProduct(int id)
    {
        var product = await _productService.GetByIdAsync(id);
        if (product == null)
        {
            return NotFound();
        }

        // Map to ViewModel
        var vm = new ProductViewModel
        {
            ProductID = product.ProductID,
            ShortCode = product.ShortCode,
            Name = product.Name,
            Category_ID = product.Category_ID,
            SubCategory_ID = product.SubCategory_ID,
            OpeningPrice = product.OpeningPrice,
            OpeningQuantity = product.OpeningQuantity,
            ReorderLevel = product.ReorderLevel,
            IsActive = product.IsActive,
            CreatedAt = product.CreatedAt,
            CreatedBy = product.CreatedBy,
            UpdatedAt = product.UpdatedAt,
            UpdatedBy = product.UpdatedBy
        };

        // Load Prices
        var priceTypes = await _productService.GetPriceTypesAsync();
        var existingPrices = await _productService.GetProductPricesAsync(id);

        foreach (var pt in priceTypes)
        {
            var existing = existingPrices.FirstOrDefault(pp => pp.PriceType_ID == pt.PriceTypeID);
            
            vm.ProductPrices.Add(new Application.DTOs.Configuration.ProductPriceDto
            {
                PriceTypeId = pt.PriceTypeID,
                PriceTypeName = pt.PriceTypeName,
                Price = existing?.SalePrice ?? 0,
                IsSelected = existing != null
            });
        }

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditProduct(int id, ProductViewModel vm)
    {
        if (id != vm.ProductID)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            var updated = await _productService.UpdateAsync(vm, CurrentUserId);
            if (!updated)
            {
                return NotFound();
            }
            
            // Save Prices
            await _productService.SaveProductPricesAsync(id, vm.ProductPrices, CurrentUserId);
            
            ShowMessage(MessageType.Success, "Product updated successfully!");
            return RedirectToAction("ProductsIndex");
        }
        
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _productService.ToggleStatusAsync(id, CurrentUserId);
        ShowMessage(MessageType.Success, "Product status updated successfully!");
        return RedirectToAction("ProductsIndex");
    }
    [HttpGet]
    public async Task<IActionResult> GetSubCategoriesByCategoryId(int categoryId)
    {
        var subCategories = await _productService.GetSubCategoriesByCategoryIdAsync(categoryId);
        return Json(subCategories.Select(s => new { id = s.SubCategoryID, name = s.Name }));
    }
}
