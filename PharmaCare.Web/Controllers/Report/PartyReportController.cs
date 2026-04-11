using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCare.Application.Interfaces.Reports;
using PharmaCare.Application.ViewModels.Report;

namespace PharmaCare.Web.Controllers.Report;

[Authorize]
public class PartyReportController : BaseController
{
    private readonly IFinancialReportService _financialReportService;

    public PartyReportController(IFinancialReportService financialReportService)
    {
        _financialReportService = financialReportService;
    }

    public IActionResult PartyReportIndex() => View();

    public async Task<IActionResult> CustomerLedger(DateRangeFilter? filter)
    {
        filter ??= new DateRangeFilter();
        var vm = await _financialReportService.GetPartyLedgerAsync(filter, "Customer");
        return View(vm);
    }

    public async Task<IActionResult> SupplierLedger(DateRangeFilter? filter)
    {
        filter ??= new DateRangeFilter();
        var vm = await _financialReportService.GetPartyLedgerAsync(filter, "Supplier");
        return View(vm);
    }
}
