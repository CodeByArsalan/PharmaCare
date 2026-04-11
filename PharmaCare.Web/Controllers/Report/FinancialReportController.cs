using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCare.Application.Interfaces.Reports;
using PharmaCare.Application.ViewModels.Report;

namespace PharmaCare.Web.Controllers.Report;

[Authorize]
public class FinancialReportController : BaseController
{
    private readonly IFinancialReportService _financialReportService;

    public FinancialReportController(IFinancialReportService financialReportService)
    {
        _financialReportService = financialReportService;
    }

    public IActionResult FinancialReportIndex() => View();

    public async Task<IActionResult> ProfitLoss(DateRangeFilter? filter)
    {
        filter ??= new DateRangeFilter();
        var vm = await _financialReportService.GetProfitLossAsync(filter);
        return View(vm);
    }

    public async Task<IActionResult> CashFlow(DateRangeFilter? filter)
    {
        filter ??= new DateRangeFilter();
        var vm = await _financialReportService.GetCashFlowReportAsync(filter);
        return View(vm);
    }

    public async Task<IActionResult> ReceivablesAging(DateTime? asOfDate)
    {
        var date = asOfDate ?? DateTime.Today;
        var vm = await _financialReportService.GetReceivablesAgingAsync(date);
        return View(vm);
    }

    public async Task<IActionResult> PayablesAging(DateTime? asOfDate)
    {
        var date = asOfDate ?? DateTime.Today;
        var vm = await _financialReportService.GetPayablesAgingAsync(date);
        return View(vm);
    }

    public async Task<IActionResult> ExpenseReport(DateRangeFilter? filter)
    {
        filter ??= new DateRangeFilter();
        var vm = await _financialReportService.GetExpenseReportAsync(filter);
        return View(vm);
    }

    public async Task<IActionResult> TrialBalance(DateTime? asOfDate)
    {
        var date = asOfDate ?? DateTime.Today;
        var vm = await _financialReportService.GetTrialBalanceAsync(date);
        return View(vm);
    }

    public async Task<IActionResult> GeneralLedger(DateRangeFilter? filter)
    {
        filter ??= new DateRangeFilter();
        var vm = await _financialReportService.GetGeneralLedgerAsync(filter);
        return View(vm);
    }
}
