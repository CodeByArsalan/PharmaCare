using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCare.Application.Interfaces.Reports;
using PharmaCare.Application.ViewModels.Report;

namespace PharmaCare.Web.Controllers.Report;

[Authorize]
public class SalesReportController : BaseController
{
    private readonly ISalesReportService _salesReportService;

    public SalesReportController(ISalesReportService salesReportService)
    {
        _salesReportService = salesReportService;
    }

    public IActionResult SalesReportIndex() => View();

    public async Task<IActionResult> DailySalesSummary(DateTime? date)
    {
        var reportDate = date ?? DateTime.Today;
        var vm = await _salesReportService.GetDailySalesSummaryAsync(reportDate);
        return View(vm);
    }

    public async Task<IActionResult> SalesReport(DateRangeFilter? filter)
    {
        filter ??= new DateRangeFilter();
        var vm = await _salesReportService.GetSalesReportAsync(filter);
        return View(vm);
    }

    public async Task<IActionResult> SalesByProduct(DateRangeFilter? filter)
    {
        filter ??= new DateRangeFilter();
        var vm = await _salesReportService.GetSalesByProductAsync(filter);
        return View(vm);
    }

    public async Task<IActionResult> SalesByCustomer(DateRangeFilter? filter)
    {
        filter ??= new DateRangeFilter();
        var vm = await _salesReportService.GetSalesByCustomerAsync(filter);
        return View(vm);
    }

    public async Task<IActionResult> CustomerBalanceSummary(DateTime? asOfDate)
    {
        var date = asOfDate ?? DateTime.Today;
        var vm = await _salesReportService.GetCustomerBalanceSummaryAsync(date);
        return View(vm);
    }
}
