using Microsoft.AspNetCore.Mvc;
using PharmaCare.Application.Interfaces.SaleManagement;
using PharmaCare.Application.DTOs.POS;
using PharmaCare.Domain.Models.SaleManagement;
using PharmaCare.Web.Utilities;
using PharmaCare.Web.Extensions;

namespace PharmaCare.Web.Controllers.SaleManagement;

public class HeldSaleController : BaseController
{
    private readonly IHeldSaleService _heldSaleService;

    public HeldSaleController(IHeldSaleService heldSaleService)
    {
        _heldSaleService = heldSaleService;
    }
    public async Task<IActionResult> HeldSaleIndex()
    {
        var heldSales = await _heldSaleService.GetHeldSales(LoginUserStoreID ?? 0);
        return View(heldSales);
    }
    [HttpPost]
    public async Task<IActionResult> HoldSale([FromBody] HoldSaleRequest request)
    {
        try
        {
            var heldSale = new HeldSale
            {
                Store_ID = LoginUserStoreID ?? 0,
                Party_ID = request.CustomerId,
                CustomerName = request.CustomerName,
                CustomerPhone = request.CustomerPhone,
                Notes = request.Notes,
                HeldLines = request.Items.Select(i => new HeldSaleLine
                {
                    Product_ID = i.ProductID,
                    ProductBatch_ID = i.ProductBatchID,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    DiscountPercent = i.DiscountPercent,
                    DiscountAmount = i.DiscountAmount
                }).ToList()
            };

            int heldSaleId = await _heldSaleService.HoldSale(heldSale, LoginUserID);
            return Json(new { success = true, heldSaleId, message = "Sale parked successfully." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }
    [HttpGet]
    public async Task<IActionResult> ResumeSale(string id)
    {
        try
        {
            int decryptedId = DecryptId(id);
            var cartItems = await _heldSaleService.ResumeHeldSale(decryptedId);

            // Store cart items in session and redirect to POS
            if (cartItems.Any())
            {
                HttpContext.Session.SetObjectAsJson("Cart", cartItems);
                ShowMessage(MessageBox.Success, $"Held sale resumed with {cartItems.Count} items.");
            }
            else
            {
                ShowMessage(MessageBox.Warning, "No items could be restored. Stock may have changed.");
            }

            return RedirectToAction("Index", "Pos");
        }
        catch (Exception ex)
        {
            ShowMessage(MessageBox.Error, ex.Message);
            return RedirectToAction(nameof(HeldSaleIndex));
        }
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteHeldSale(string id)
    {
        try
        {
            int decryptedId = DecryptId(id);
            if (await _heldSaleService.DeleteHeldSale(decryptedId, LoginUserID))
            {
                ShowMessage(MessageBox.Success, "Held sale deleted successfully.");
            }
            else
            {
                ShowMessage(MessageBox.Error, "Failed to delete held sale.");
            }
        }
        catch (Exception ex)
        {
            ShowMessage(MessageBox.Error, ex.Message);
        }

        return RedirectToAction(nameof(HeldSaleIndex));
    }
    public async Task<IActionResult> HeldSaleDetails(string id)
    {
        int decryptedId = DecryptId(id);
        var heldSale = await _heldSaleService.GetHeldSaleById(decryptedId);
        if (heldSale == null)
            return NotFound();

        return View(heldSale);
    }
    [HttpGet]
    public async Task<IActionResult> GetHeldSalesCount()
    {
        var heldSales = await _heldSaleService.GetHeldSales(LoginUserStoreID ?? 0);
        return Json(new { count = heldSales.Count() });
    }
}

// Request model for holding sales from POS
public class HoldSaleRequest
{
    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? Notes { get; set; }
    public List<CartItemDto> Items { get; set; } = new();
}
