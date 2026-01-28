using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaCare.Application.Interfaces.SaleManagement;
using PharmaCare.Application.Implementations.SaleManagement;
using PharmaCare.Web.Utilities;

namespace PharmaCare.Web.Controllers.SaleManagement;

public class SalesReturnController : BaseController
{
    private readonly ISalesReturnService _returnService;
    private readonly IPosService _posService;

    public SalesReturnController(
        ISalesReturnService returnService,
        IPosService posService)
    {
        _returnService = returnService;
        _posService = posService;
    }

    public async Task<IActionResult> SalesReturnIndex(DateTime? startDate = null, DateTime? endDate = null)
    {
        startDate ??= DateTime.Today.AddDays(-30);
        endDate ??= DateTime.Today;

        ViewBag.StartDate = startDate.Value.ToString("yyyy-MM-dd");
        ViewBag.EndDate = endDate.Value.ToString("yyyy-MM-dd");

        var returns = await _returnService.GetReturns(startDate, endDate, LoginUserStoreID ?? 0);
        return View(returns);
    }

    [HttpGet]
    public async Task<IActionResult> AddSalesReturn(string? saleId)
    {
        if (string.IsNullOrEmpty(saleId))
        {
            ShowMessage(MessageBox.Error, "Sale ID is required.");
            return RedirectToAction(nameof(SalesReturnIndex));
        }

        int decryptedSaleId = DecryptId(saleId);
        var sale = await _returnService.GetSaleForReturn(decryptedSaleId);

        if (sale == null)
        {
            ShowMessage(MessageBox.Error, "Sale not found.");
            return RedirectToAction(nameof(SalesReturnIndex));
        }

        if (sale.Status == "Voided")
        {
            ShowMessage(MessageBox.Error, "Cannot return items from a voided sale.");
            return RedirectToAction(nameof(SalesReturnIndex));
        }

        ViewBag.Sale = sale;
        ViewBag.RefundMethods = new SelectList(new[] { "Cash", "Credit" }, "Cash");
        ViewBag.ReturnReasons = new SelectList(new[] { "Defective", "WrongItem", "Expired", "ChangeOfMind", "Other" });

        var viewModel = new AddSalesReturnViewModel
        {
            OriginalSaleId = sale.StockMainID,
            StoreId = LoginUserStoreID ?? 0
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSalesReturn(AddSalesReturnViewModel viewModel)
    {
        try
        {
            var request = new CreateSalesReturnRequest
            {
                OriginalSaleId = viewModel.OriginalSaleId,
                StoreId = viewModel.StoreId,
                ReturnReason = viewModel.ReturnReason,
                RefundMethod = viewModel.RefundMethod,
                Lines = viewModel.Lines?.Select(l => new CreateSalesReturnLineRequest
                {
                    ProductBatchId = l.ProductBatchId,
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice,
                    Reason = l.Reason,
                    RestockInventory = l.RestockInventory
                }).ToList() ?? new()
            };

            int returnId = await _returnService.CreateReturn(request, LoginUserID);
            ShowMessage(MessageBox.Success, "Sales return processed successfully.");
            return RedirectToAction(nameof(SalesReturnDetails), new { id = Utility.EncryptURL(returnId.ToString()) });
        }
        catch (InvalidOperationException ex)
        {
            ShowMessage(MessageBox.Error, ex.Message);
        }
        catch (Exception ex)
        {
            ShowMessage(MessageBox.Error, $"An error occurred: {ex.Message}");
        }

        // Reload sale for view
        var sale = await _returnService.GetSaleForReturn(viewModel.OriginalSaleId);
        ViewBag.Sale = sale;
        ViewBag.RefundMethods = new SelectList(new[] { "Cash", "Credit" }, viewModel.RefundMethod);
        ViewBag.ReturnReasons = new SelectList(new[] { "Defective", "WrongItem", "Expired", "ChangeOfMind", "Other" }, viewModel.ReturnReason);

        return View(viewModel);
    }

    public async Task<IActionResult> SalesReturnDetails(string id)
    {
        int decryptedId = DecryptId(id);
        var salesReturn = await _returnService.GetReturnById(decryptedId);

        if (salesReturn == null)
            return NotFound();

        return View(salesReturn);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelSalesReturn(string id)
    {
        try
        {
            int decryptedId = DecryptId(id);
            if (await _returnService.CancelReturn(decryptedId, LoginUserID))
            {
                ShowMessage(MessageBox.Success, "Return cancelled successfully.");
            }
            else
            {
                ShowMessage(MessageBox.Error, "Failed to cancel return.");
            }
        }
        catch (InvalidOperationException ex)
        {
            ShowMessage(MessageBox.Error, ex.Message);
        }

        return RedirectToAction(nameof(SalesReturnIndex));
    }

    public async Task<IActionResult> SelectSale(DateTime? startDate = null, DateTime? endDate = null)
    {
        startDate ??= DateTime.Today.AddDays(-7);
        endDate ??= DateTime.Today;

        ViewBag.StartDate = startDate.Value.ToString("yyyy-MM-dd");
        ViewBag.EndDate = endDate.Value.ToString("yyyy-MM-dd");

        var sales = await _posService.GetSalesHistory(startDate, endDate, LoginUserStoreID ?? 0);
        var validSales = sales.Where(s => s.Status != "Voided").ToList();
        return View(validSales);
    }
}

/// <summary>
/// ViewModel for creating a sales return
/// </summary>
public class AddSalesReturnViewModel
{
    public int OriginalSaleId { get; set; }
    public int StoreId { get; set; }
    public string? ReturnReason { get; set; }
    public string RefundMethod { get; set; } = "Cash";
    public List<AddSalesReturnLineViewModel> Lines { get; set; } = new();
}

public class AddSalesReturnLineViewModel
{
    public int ProductBatchId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string? Reason { get; set; }
    public bool RestockInventory { get; set; } = true;
}
