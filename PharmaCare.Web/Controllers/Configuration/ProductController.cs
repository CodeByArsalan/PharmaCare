using Microsoft.AspNetCore.Mvc;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Domain.Models.Products;

namespace PharmaCare.Web.Controllers.Configuration;

public class ProductController(IProductService _productService, IBarcodeService _barcodeService) : BaseController
{
    public async Task<IActionResult> ProductIndex()
    {
        try
        {
            var products = await _productService.GetProducts();
            return View(products);
        }
        catch (Exception ex)
        {
            ShowMessage(MessageBox.Error, ex.Message);
            return View(new List<Product>());
        }
    }
    [HttpGet]
    public IActionResult AddProduct()
    {
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> AddProduct(Product product)
    {
        if (ModelState.IsValid)
        {
            try
            {
                // Ensure helper properties are set if BaseModel requires them
                product.CreatedBy = LoginUserID; // Or get from User.Identity

                if (await _productService.CreateProduct(product))
                {
                    ShowMessage(MessageBox.Success, "Product created successfully.");
                    return RedirectToAction(nameof(ProductIndex));
                }
                else
                {
                    ShowMessage(MessageBox.Error, "Failed to create product.");
                }
            }
            catch (Exception ex)
            {
                ShowMessage(MessageBox.Error, ex.Message);
            }
        }
        return View(product);
    }
    [HttpGet]
    public async Task<IActionResult> EditProduct(string id)
    {
        try
        {
            int decryptedId = DecryptId(id);
            var product = await _productService.GetProductById(decryptedId);
            if (product == null)
            {
                return NotFound();
            }
            return View(product);
        }
        catch (Exception ex)
        {
            ShowMessage(MessageBox.Error, ex.Message);
            return RedirectToAction(nameof(ProductIndex));
        }
    }
    [HttpPost]
    public async Task<IActionResult> EditProduct(Product product)
    {
        if (ModelState.IsValid)
        {
            try
            {
                product.UpdatedBy = LoginUserID;
                if (await _productService.UpdateProduct(product))
                {
                    ShowMessage(MessageBox.Success, "Product updated successfully.");
                    return RedirectToAction(nameof(ProductIndex));
                }
                else
                {
                    ShowMessage(MessageBox.Error, "Failed to update product.");
                }
            }
            catch (Exception ex)
            {
                ShowMessage(MessageBox.Error, ex.Message);
            }
        }
        return View(product);
    }
    [HttpPost]
    public async Task<IActionResult> DeleteProduct(string id)
    {
        try
        {
            int decryptedId = DecryptId(id);
            if (await _productService.DeleteProduct(decryptedId))
            {
                ShowMessage(MessageBox.Success, "Product deactivated successfully.");
            }
            else
            {
                ShowMessage(MessageBox.Error, "Failed to deactivate product.");
            }
        }
        catch (Exception ex)
        {
            ShowMessage(MessageBox.Error, ex.Message);
        }
        return RedirectToAction(nameof(ProductIndex));
    }
    [HttpGet]
    public IActionResult GenerateBarcode()
    {
        try
        {
            var barcode = _barcodeService.GenerateRandomBarcode();
            return Json(new { success = true, barcode });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }
}
