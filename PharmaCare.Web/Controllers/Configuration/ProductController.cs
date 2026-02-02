using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
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

    public async Task<IActionResult> ProductsIndex(int? categoryId, int? subCategoryId, int? status, string? searchTerm, string? activeTab)
    {
        // Populate Dropdowns for filter
        var categories = await _productService.GetCategoriesForDropdownAsync();
        ViewBag.Categories = new SelectList(categories, "CategoryID", "Name", categoryId);

        if (categoryId.HasValue)
        {
            var subCategories = await _productService.GetSubCategoriesByCategoryIdAsync(categoryId.Value);
            ViewBag.SubCategories = new SelectList(subCategories, "SubCategoryID", "Name", subCategoryId);
        }
        else
        {
            ViewBag.SubCategories = new SelectList(Enumerable.Empty<SelectListItem>());
        }

        // Status Logic: 1 = Active, 0 = Inactive, null = All
        bool? isActive = status.HasValue ? (status.Value == 1) : null;
        ViewBag.CurrentStatus = status;
        ViewBag.CurrentSearch = searchTerm;
        ViewBag.CurrentCategory = categoryId;
        ViewBag.CurrentSubCategory = subCategoryId;

        // Get Products
        var products = await _productService.GetFilteredProductsAsync(categoryId, subCategoryId, isActive, searchTerm);

        // Prepare Add Product form data
        var priceTypes = await _productService.GetPriceTypesAsync();
        var newProductVm = new ProductViewModel();
        foreach (var pt in priceTypes)
        {
            newProductVm.ProductPrices.Add(new Application.DTOs.Configuration.ProductPriceDto
            {
                PriceTypeId = pt.PriceTypeID,
                PriceTypeName = pt.PriceTypeName,
                Price = 0
            });
        }

        // Combine into ProductIndexViewModel
        var vm = new ProductIndexViewModel
        {
            Products = products,
            NewProduct = newProductVm,
            ActiveTab = activeTab ?? "products"
        };

        return View("ProductsIndex", vm);
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
                Price = 0
            });
        }
        
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddProduct([Bind(Prefix = "NewProduct")] ProductViewModel vm)
    {
        if (ModelState.IsValid)
        {
            // Calculate Total Opening Quantity
            vm.OpeningQuantity = (vm.OpeningStockBoxes * vm.UnitsInPack) + vm.OpeningStockUnits;

            // Create Product
            var createdProduct = await _productService.CreateAsync(vm, CurrentUserId);
            
            // Save Prices
            await _productService.SaveProductPricesAsync(createdProduct.ProductID, vm.ProductPrices, CurrentUserId);
            
            ShowMessage(MessageType.Success, "Product created successfully!");
            return RedirectToAction("ProductsIndex", new { activeTab = "products" });
        }
        
        // On validation failure, redirect back to the index with the "add" tab active
        ShowMessage(MessageType.Error, "Please correct the validation errors and try again.");
        return RedirectToAction("ProductsIndex", new { activeTab = "add" });
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
            UnitsInPack = product.UnitsInPack,
            IsActive = product.IsActive,
            CreatedAt = product.CreatedAt,
            CreatedBy = product.CreatedBy,
            UpdatedAt = product.UpdatedAt,
            UpdatedBy = product.UpdatedBy
        };

        // Calculate Boxes and Units for display
        if (vm.UnitsInPack > 0)
        {
            vm.OpeningStockBoxes = vm.OpeningQuantity / vm.UnitsInPack;
            vm.OpeningStockUnits = vm.OpeningQuantity % vm.UnitsInPack;
        }
        else 
        {
            vm.OpeningStockBoxes = 0;
            vm.OpeningStockUnits = vm.OpeningQuantity;
        }

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
                Price = existing?.SalePrice ?? 0
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
             // Calculate Total Opening Quantity on Edit (if needed to update inventory from here, 
             // though usually Opening Stock is static after creation. 
             // Assuming user can correct it here if they made a mistake)
             vm.OpeningQuantity = (vm.OpeningStockBoxes * vm.UnitsInPack) + vm.OpeningStockUnits;

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
