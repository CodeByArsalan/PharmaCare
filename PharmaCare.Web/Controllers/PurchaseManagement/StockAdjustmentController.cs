using Microsoft.AspNetCore.Mvc;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Domain.Entities.Transactions;
using PharmaCare.Web.Filters;
using PharmaCare.Web.Utilities;
using System.Text.Json;

namespace PharmaCare.Web.Controllers.PurchaseManagement;

public class StockAdjustmentController : BaseController
{
    private readonly IStockAdjustmentService _service;

    public StockAdjustmentController(IStockAdjustmentService service)
    {
        _service = service;
    }

    public async Task<IActionResult> AdjustmentIndex(string? type, DateTime? from, DateTime? to, int page = 1)
    {
        ViewBag.FromDate = from?.ToString("yyyy-MM-dd");
        ViewBag.ToDate = to?.ToString("yyyy-MM-dd");
        ViewBag.CurrentType = type;

        var adjustments = await _service.GetPagedAsync(type, from, to, page);
        return View(adjustments);
    }

    public IActionResult AddAdjustment()
    {
        return View(new StockMain { TransactionDate = AppTime.Today });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddAdjustment(
        [Bind("TransactionDate,AdjustmentType,AdjustmentReason,Remarks")] StockMain adjustment, string detailsJson)
    {
        ModelState.Remove("TransactionType");
        ModelState.Remove("TransactionNo");
        ModelState.Remove("Party");
        ModelState.Remove("Voucher");
        ModelState.Remove("ReferenceStockMain");

        try
        {
            if (string.IsNullOrWhiteSpace(detailsJson))
                throw new InvalidOperationException("No products selected.");

            var details = JsonSerializer.Deserialize<List<StockDetail>>(detailsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (details == null || !details.Any())
                throw new InvalidOperationException("Invalid product details.");

            adjustment.StockDetails = details;
            
            if (ModelState.IsValid)
            {
                await _service.CreateAsync(adjustment, CurrentUserId);
                ShowMessage(MessageType.Success, "Stock adjustment recorded successfully.");
                return RedirectToAction(nameof(AdjustmentIndex));
            }
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", ex.Message);
        }

        return View(adjustment);
    }

    public async Task<IActionResult> ViewAdjustment(string id)
    {
        int adjId = Utility.DecryptId(id);
        if (adjId <= 0) return RedirectToAction(nameof(AdjustmentIndex));

        var adj = await _service.GetByIdAsync(adjId);
        if (adj == null) return NotFound();

        return View(adj);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [LinkedToPage("StockAdjustment", "AdjustmentIndex", PermissionType = "delete")]
    public async Task<IActionResult> Void(string id, string voidReason)
    {
        int adjId = Utility.DecryptId(id);
        if (adjId <= 0) return RedirectToAction(nameof(AdjustmentIndex));

        try
        {
            var result = await _service.VoidAsync(adjId, voidReason, CurrentUserId);
            ShowMessage(result ? MessageType.Success : MessageType.Warning,
                result ? "Stock adjustment voided successfully." : "Failed to void adjustment.");
        }
        catch (Exception ex)
        {
            ShowMessage(MessageType.Error, ex.Message);
        }

        return RedirectToAction(nameof(AdjustmentIndex));
    }
}
