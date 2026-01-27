using Microsoft.AspNetCore.Mvc;
using PharmaCare.Application.Interfaces.Inventory;

using PharmaCare.Web.Models.Inventory;

namespace PharmaCare.Web.Controllers.Inventory;

public class StockController : BaseController
{
    private readonly IStockService _stockService;

    public StockController(IStockService stockService)
    {
        _stockService = stockService;
    }

    public async Task<IActionResult> StockIndex(int? storeId = null)
    {
        var viewModel = new StockOverviewViewModel
        {
            StockItems = await _stockService.GetStockOverview(),
            Summary = await _stockService.GetInventorySummary(storeId),
            SelectedStore_ID = storeId
        };

        // Filter stock items if storeId is provided
        if (storeId.HasValue)
        {
            viewModel.StockItems = viewModel.StockItems.Where(i => i.Store_ID == storeId.Value).ToList();
        }

        return View(viewModel);
    }
}
