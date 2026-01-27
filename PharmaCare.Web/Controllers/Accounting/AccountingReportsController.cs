using Microsoft.AspNetCore.Mvc;
using PharmaCare.Application.Interfaces.AccountManagement;
using PharmaCare.Web.Controllers;

namespace PharmaCare.Web.Controllers.Accounting;

public class AccountingReportsController : BaseController
{
    private readonly IAccountingService _accountingService;

    public AccountingReportsController(IAccountingService accountingService)
    {
        _accountingService = accountingService;
    }

    // GET: AccountingReports/TrialBalance
    public async Task<IActionResult> TrialBalance(DateTime? asOfDate)
    {
        asOfDate ??= DateTime.Now;
        var trialBalance = await _accountingService.GetTrialBalance(asOfDate.Value);

        ViewBag.AsOfDate = asOfDate.Value.ToString("yyyy-MM-dd");

        return View(trialBalance);
    }

    // GET: AccountingReports/BalanceSheet
    public async Task<IActionResult> BalanceSheet(DateTime? asOfDate)
    {
        asOfDate ??= DateTime.Now;
        var balanceSheet = await _accountingService.GetBalanceSheet(asOfDate.Value);

        ViewBag.AsOfDate = asOfDate.Value.ToString("yyyy-MM-dd");

        return View(balanceSheet);
    }

    // GET: AccountingReports/IncomeStatement
    public async Task<IActionResult> IncomeStatement(DateTime? fromDate, DateTime? toDate)
    {
        // Default to current month
        fromDate ??= new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        toDate ??= DateTime.Now;

        var incomeStatement = await _accountingService.GetIncomeStatement(fromDate.Value, toDate.Value);

        ViewBag.FromDate = fromDate.Value.ToString("yyyy-MM-dd");
        ViewBag.ToDate = toDate.Value.ToString("yyyy-MM-dd");

        return View(incomeStatement);
    }

    // GET: AccountingReports/GeneralLedger
    public async Task<IActionResult> GeneralLedger(int? accountId, DateTime? fromDate, DateTime? toDate)
    {
        if (!accountId.HasValue)
        {
            // Show account selection page
            var accounts = await _accountingService.GetChartOfAccounts(true);
            return View("SelectAccount", accounts);
        }

        // Default to current month
        fromDate ??= new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        toDate ??= DateTime.Now;

        var generalLedger = await _accountingService.GetGeneralLedger(accountId.Value, fromDate, toDate);

        ViewBag.FromDate = fromDate.Value.ToString("yyyy-MM-dd");
        ViewBag.ToDate = toDate.Value.ToString("yyyy-MM-dd");

        return View(generalLedger);
    }

    // GET: AccountingReports/AccountBalance - AJAX for real-time balance
    [HttpGet]
    public async Task<IActionResult> GetAccountBalance(int accountId, DateTime? asOfDate)
    {
        var balance = await _accountingService.GetAccountBalance(accountId, asOfDate);
        return Json(new { balance = balance.ToString("N2") });
    }
}
