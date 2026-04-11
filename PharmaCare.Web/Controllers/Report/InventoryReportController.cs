using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCare.Application.Interfaces.Reports;
using PharmaCare.Application.ViewModels.Report;

namespace PharmaCare.Web.Controllers.Report;

[Authorize]
public class InventoryReportController : BaseController
{
    private readonly IInventoryReportService _inventoryReportService;

    public InventoryReportController(IInventoryReportService inventoryReportService)
    {
        _inventoryReportService = inventoryReportService;
    }

    public IActionResult InventoryReportIndex() => View();

    public async Task<IActionResult> CurrentStock(DateRangeFilter? filter)
    {
        filter ??= new DateRangeFilter();
        var vm = await _inventoryReportService.GetCurrentStockReportAsync(filter);
        return View(vm);
    }

    public async Task<IActionResult> LowStock(DateRangeFilter? filter)
    {
        filter ??= new DateRangeFilter();
        var vm = await _inventoryReportService.GetLowStockReportAsync(filter);
        return View(vm);
    }

    public async Task<IActionResult> ProductMovement(DateRangeFilter? filter)
    {
        filter ??= new DateRangeFilter();
        var vm = await _inventoryReportService.GetProductMovementReportAsync(filter);
        return View(vm);
    }

    public async Task<IActionResult> DeadStock(DateRangeFilter? filter)
    {
        filter ??= new DateRangeFilter { ThresholdDays = 30 };
        var vm = await _inventoryReportService.GetDeadStockReportAsync(filter);
        return View(vm);
    }
}
