using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCare.Application.Interfaces.Reports;
using PharmaCare.Application.ViewModels.Report;

namespace PharmaCare.Web.Controllers;

/// <summary>
/// Home controller for dashboard.
/// Inherits from BaseController but the filter skips Home controller by default.
/// </summary>
public class HomeController : BaseController
{
    private readonly ISalesReportService _salesReportService;
    private readonly IInventoryReportService _inventoryReportService;

    public HomeController(
        ISalesReportService salesReportService,
        IInventoryReportService inventoryReportService)
    {
        _salesReportService = salesReportService;
        _inventoryReportService = inventoryReportService;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            // Fetch today's sales summary
            var dailySummary = await _salesReportService.GetDailySalesSummaryAsync(DateTime.Today);
            ViewBag.TodaySales = dailySummary.TotalSales;
            ViewBag.TodayOrders = dailySummary.TransactionCount;
            ViewBag.ItemsSold = dailySummary.ItemsSold;
            ViewBag.CashCollected = dailySummary.CashCollected;

            // Fetch inventory summary
            var stockFilter = new DateRangeFilter();
            var stockReport = await _inventoryReportService.GetCurrentStockReportAsync(stockFilter);
            ViewBag.TotalProducts = stockReport.TotalProducts;
            ViewBag.LowStockCount = stockReport.LowStockCount;
        }
        catch
        {
            // Fallback to zeros if reports fail (e.g., no data yet)
            ViewBag.TodaySales = 0m;
            ViewBag.TodayOrders = 0;
            ViewBag.ItemsSold = 0;
            ViewBag.CashCollected = 0m;
            ViewBag.TotalProducts = 0;
            ViewBag.LowStockCount = 0;
        }

        return View();
    }

    public IActionResult Error()
    {
        return View();
    }
}
