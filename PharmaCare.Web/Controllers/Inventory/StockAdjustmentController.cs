using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Application.Interfaces.Inventory;
using PharmaCare.Domain.Models.Inventory;
using PharmaCare.Web.Utilities;

namespace PharmaCare.Web.Controllers.Inventory;

public class StockAdjustmentController : BaseController
{
    private readonly IStockService _stockService;
    private readonly IStoreService _storeService;

    public StockAdjustmentController(IStockService stockService, IStoreService storeService)
    {
        _stockService = stockService;
        _storeService = storeService;
    }

    public async Task<IActionResult> StockAdjustmentIndex()
    {
        var adjustments = await _stockService.GetStockAdjustments();
        return View(adjustments);
    }

    [HttpGet]
    public IActionResult AddStockAdjustment()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> AddStockAdjustment(StockAdjustment adjustment)
    {
        adjustment.CreatedBy = adjustment.AdjustedBy = LoginUserID;
        if (ModelState.IsValid)
        {
            if (await _stockService.AdjustStock(adjustment))
            {
                ShowMessage(MessageBox.Success, "Stock adjusted successfully.");
                return RedirectToAction(nameof(StockAdjustmentIndex));
            }
            else
            {
                ShowMessage(MessageBox.Error, "Failed to adjust stock. Insufficient quantity or invalid batch.");
            }
        }
        return View(adjustment);
    }

    public async Task<IActionResult> StockAdjustmentDetails(int id)
    {
        var adjustment = await _stockService.GetStockAdjustmentById(id);
        if (adjustment == null) return NotFound();
        return View(adjustment);
    }

    [HttpGet]
    public async Task<IActionResult> SearchBatches(string query, int? storeId)
    {
        var results = await _stockService.SearchProductBatchesAsync(query, storeId);
        return Json(results);
    }
}
