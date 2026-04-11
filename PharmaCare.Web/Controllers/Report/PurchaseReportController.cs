using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCare.Application.Interfaces.Reports;
using PharmaCare.Application.ViewModels.Report;

namespace PharmaCare.Web.Controllers.Report;

[Authorize]
public class PurchaseReportController : BaseController
{
    private readonly IPurchaseReportService _purchaseReportService;

    public PurchaseReportController(IPurchaseReportService purchaseReportService)
    {
        _purchaseReportService = purchaseReportService;
    }

    public IActionResult PurchaseReportIndex() => View();

    public async Task<IActionResult> PurchaseReport(DateRangeFilter? filter)
    {
        filter ??= new DateRangeFilter();
        var vm = await _purchaseReportService.GetPurchaseReportAsync(filter);
        return View(vm);
    }

    public async Task<IActionResult> PurchaseBySupplier(DateRangeFilter? filter)
    {
        filter ??= new DateRangeFilter();
        var vm = await _purchaseReportService.GetPurchaseBySupplierAsync(filter);
        return View(vm);
    }
}
