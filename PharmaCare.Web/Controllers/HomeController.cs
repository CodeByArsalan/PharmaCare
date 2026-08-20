using System.Diagnostics;
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
    private readonly IPurchaseReportService _purchaseReportService;
    private readonly IFinancialReportService _financialReportService;

    public HomeController(
        ISalesReportService salesReportService,
        IInventoryReportService inventoryReportService,
        IPurchaseReportService purchaseReportService,
        IFinancialReportService financialReportService)
    {
        _salesReportService = salesReportService;
        _inventoryReportService = inventoryReportService;
        _purchaseReportService = purchaseReportService;
        _financialReportService = financialReportService;
    }

    public async Task<IActionResult> Index()
    {
        var vm = new DashboardVM();

        try
        {
            // Capability Flags Setup - utilizing dynamic permissions mapped to roles
            vm.CanViewSales = HasPermission("SalesReport", "SalesReportIndex", "view") || HasPermission("Sale", "SalesIndex", "view");
            vm.CanViewInventory = HasPermission("InventoryReport", "InventoryReportIndex", "view") || HasPermission("Product", "ProductsIndex", "view");
            vm.CanViewFinancials = HasPermission("FinancialReport", "FinancialReportIndex", "view") || HasPermission("JournalVoucher", "JournalVoucherIndex", "view");

            // Deliberately NO role-name shortcut here. The old `name.Contains("Admin")` check
            // meant a role called "Admin Assistant" — or anything else with those five letters —
            // unlocked every financial figure on the dashboard regardless of its actual
            // permissions. A real administrator's role GRANTS the report pages, so the permission
            // checks above already say yes for them; the page permissions are the only authority.

            // 1. Fetch today's sales summary
            if (vm.CanViewSales || vm.CanViewFinancials)
            {
                var dailySummary = await _salesReportService.GetDailySalesSummaryAsync(AppTime.Today);
                vm.TodaySalesSummary = dailySummary;
                vm.TodaySales = dailySummary.TotalSales;
                vm.TodayOrders = dailySummary.TransactionCount;
                vm.ItemsSold = dailySummary.ItemsSold;
                vm.CashCollected = dailySummary.CashCollected;
                vm.GrossProfit = dailySummary.GrossProfit;
                vm.OutstandingBalance = dailySummary.OutstandingBalance;
            }

            // 2. Fetch weekly sales and purchases for chart
            if (vm.CanViewFinancials)
            {
                var weekFilter = new DateRangeFilter { FromDate = AppTime.Today.AddDays(-6), ToDate = AppTime.Today };
                var weeklyReport = await _salesReportService.GetSalesReportAsync(weekFilter);
                var weeklyPurchaseReport = await _purchaseReportService.GetPurchaseReportAsync(weekFilter);
                
                var byDaySales = weeklyReport.Rows
                    .GroupBy(r => r.TransactionDate.Date)
                    .Select(g => new { Date = g.Key, Total = g.Sum(r => r.TotalAmount) })
                    .ToList();

                var byDayPurchases = weeklyPurchaseReport.Rows
                    .GroupBy(r => r.TransactionDate.Date)
                    .Select(g => new { Date = g.Key, Total = g.Sum(r => r.TotalAmount) })
                    .ToList();

                var last7Days = Enumerable.Range(0, 7)
                    .Select(i => AppTime.Today.AddDays(-6 + i))
                    .ToList();

                var chartLabels = last7Days.Select(d => d.ToString("ddd")).ToList();
                var chartSalesData = last7Days.Select(d => byDaySales.FirstOrDefault(x => x.Date == d)?.Total ?? 0m).ToList();
                var chartPurchaseData = last7Days.Select(d => byDayPurchases.FirstOrDefault(x => x.Date == d)?.Total ?? 0m).ToList();

                vm.WeeklyLabelsJson = System.Text.Json.JsonSerializer.Serialize(chartLabels);
                vm.WeeklySalesDataJson = System.Text.Json.JsonSerializer.Serialize(chartSalesData);
                vm.WeeklyPurchaseDataJson = System.Text.Json.JsonSerializer.Serialize(chartPurchaseData);
            }

            // Recent activity removed to improve dashboard load time

            // 4. Fetch inventory summary and low stock
            if (vm.CanViewInventory || vm.CanViewSales)
            {
                var stockFilter = new DateRangeFilter();
                var stockReport = await _inventoryReportService.GetCurrentStockReportAsync(stockFilter);
                vm.TotalProducts = stockReport.TotalProducts;
                vm.LowStockCount = stockReport.LowStockCount;

                var lowStockReport = await _inventoryReportService.GetLowStockReportAsync(stockFilter);
                vm.CriticalStock = lowStockReport.Rows.OrderByDescending(r => r.Shortfall).Take(5).ToList();
            }

            // 5. Fetch Top 5 Products by Revenue (This Month)
            if (vm.CanViewSales || vm.CanViewFinancials)
            {
                var monthFilter = new DateRangeFilter { FromDate = new DateTime(AppTime.Today.Year, AppTime.Today.Month, 1), ToDate = AppTime.Today };
                var salesByProduct = await _salesReportService.GetSalesByProductAsync(monthFilter);
                vm.TopProducts = salesByProduct.Rows.OrderByDescending(r => r.Revenue).Take(5).ToList();
                
                vm.TopProductsLabelsJson = System.Text.Json.JsonSerializer.Serialize(vm.TopProducts.Select(p => p.ProductName));
                vm.TopProductsDataJson = System.Text.Json.JsonSerializer.Serialize(vm.TopProducts.Select(p => p.Revenue));
            }

            // 6. Fetch Customer Outstanding Balance Summary
            if (vm.CanViewFinancials)
            {
                var customerBalances = await _salesReportService.GetCustomerBalanceSummaryAsync(AppTime.Today);
                vm.TopOutstandingCustomers = customerBalances.Rows.OrderByDescending(r => r.BalanceDue).Take(5).ToList();
                
                vm.TopCustomersLabelsJson = System.Text.Json.JsonSerializer.Serialize(vm.TopOutstandingCustomers.Select(c => c.CustomerName));
                vm.TopCustomersDataJson = System.Text.Json.JsonSerializer.Serialize(vm.TopOutstandingCustomers.Select(c => c.BalanceDue));
            }

            // 7. Today's Profit/Loss and Cash Flow
            if (vm.CanViewFinancials)
            {
                var todayFilter = new DateRangeFilter { FromDate = AppTime.Today, ToDate = AppTime.Today };
                vm.TodayProfitLoss = await _financialReportService.GetProfitLossAsync(todayFilter);
                vm.TodayCashFlow = await _financialReportService.GetCashFlowReportAsync(todayFilter);
            }
        }
        catch (Exception ex)
        {
            // Defaults are kept so the shell still renders, but the failure must be both logged
            // and surfaced — a silent dashboard of zeros reads as a quiet day, not an outage.
            Logger.LogError(ex, "Dashboard load failed for user {UserId} (pharmacy {PharmacyId}).",
                CurrentUserId, CurrentPharmacyId);
            vm.LoadFailed = true;
        }

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> GetChartData(string period)
    {
        // Same gate as the chart section of Index: this endpoint returns tenant-wide
        // sales/purchase totals, which not every authenticated user may see. The Home
        // controller is exempt from the page-authorization filter, so gate it here.
        // Page permissions only — no role-NAME shortcut (see Index). A role merely named
        // "Admin Assistant" must not unlock tenant-wide financial totals.
        var canViewFinancials = HasPermission("FinancialReport", "FinancialReportIndex", "view")
            || HasPermission("JournalVoucher", "JournalVoucherIndex", "view");

        if (!canViewFinancials)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You do not have permission to view financial chart data." });
        }

        var filter = new DateRangeFilter();
        if (period == "7d") { filter.FromDate = AppTime.Today.AddDays(-6); filter.ToDate = AppTime.Today; }
        else if (period == "30d") { filter.FromDate = AppTime.Today.AddDays(-29); filter.ToDate = AppTime.Today; }
        else if (period == "month") { filter.FromDate = new DateTime(AppTime.Today.Year, AppTime.Today.Month, 1); filter.ToDate = AppTime.Today; }
        else if (period == "year") { filter.FromDate = new DateTime(AppTime.Today.Year, 1, 1); filter.ToDate = AppTime.Today; }
        else { filter.FromDate = AppTime.Today.AddDays(-6); filter.ToDate = AppTime.Today; }

        var salesReport = await _salesReportService.GetSalesReportAsync(filter);
        var purchaseReport = await _purchaseReportService.GetPurchaseReportAsync(filter);

        var days = (filter.ToDate - filter.FromDate).Days + 1;
        var dateRange = Enumerable.Range(0, days).Select(i => filter.FromDate.AddDays(i)).ToList();

        var byDaySales = salesReport.Rows.GroupBy(r => r.TransactionDate.Date).Select(g => new { Date = g.Key, Total = g.Sum(r => r.TotalAmount) }).ToList();
        var byDayPurchases = purchaseReport.Rows.GroupBy(r => r.TransactionDate.Date).Select(g => new { Date = g.Key, Total = g.Sum(r => r.TotalAmount) }).ToList();

        var labels = dateRange.Select(d => d.ToString("dd MMM")).ToList();
        var salesData = dateRange.Select(d => byDaySales.FirstOrDefault(x => x.Date == d)?.Total ?? 0m).ToList();
        var purchaseData = dateRange.Select(d => byDayPurchases.FirstOrDefault(x => x.Date == d)?.Total ?? 0m).ToList();

        return Json(new { labels, salesData, purchaseData });
    }

    /// <summary>
    /// Terminal error page for BOTH pipeline hooks in Program.cs: UseExceptionHandler (unhandled
    /// exception, statusCode is null) and UseStatusCodePagesWithReExecute (statusCode supplied).
    /// The Error view is deliberately layout-free so it still renders when the failure is the
    /// database or session itself — rendering _Layout here would re-throw inside the handler.
    /// </summary>
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [AllowAnonymous]
    public IActionResult Error(int? statusCode = null)
    {
        if (statusCode.HasValue && statusCode.Value == 404)
        {
            return View("NotFound");
        }

        ViewData["StatusCode"] = statusCode;
        // Correlates the page the user sees with the logged exception.
        ViewData["RequestId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        return View();
    }
}
