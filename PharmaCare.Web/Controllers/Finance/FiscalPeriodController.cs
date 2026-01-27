using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCare.Infrastructure.Interfaces.Accounting;

namespace PharmaCare.Web.Controllers.Finance;

public class FiscalPeriodController : BaseController
{
    private readonly IFiscalPeriodService _fiscalPeriodService;

    public FiscalPeriodController(IFiscalPeriodService fiscalPeriodService)
    {
        _fiscalPeriodService = fiscalPeriodService;
    }
    public async Task<IActionResult> FiscalPeriodIndex()
    {
        var fiscalYears = await _fiscalPeriodService.GetFiscalYearsAsync();
        return View(fiscalYears);
    }
    public async Task<IActionResult> Periods(int fiscalYearId)
    {
        var periods = await _fiscalPeriodService.GetPeriodsForYearAsync(fiscalYearId);
        ViewBag.FiscalYearId = fiscalYearId;
        return View(periods);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClosePeriod(int periodId, int? storeId)
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "1");
        var result = await _fiscalPeriodService.ClosePeriodAsync(periodId, userId, storeId);

        if (result.Success)
        {
            TempData["Success"] = $"Period closed successfully.";
        }
        else
        {
            TempData["Error"] = result.ErrorMessage;
        }

        return RedirectToAction(nameof(FiscalPeriodIndex));
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReopenPeriod(int periodId, string reason, int? storeId)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData["Error"] = "Reason is required to reopen a period.";
            return RedirectToAction(nameof(FiscalPeriodIndex));
        }

        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "1");
        var result = await _fiscalPeriodService.ReopenPeriodAsync(periodId, userId, reason, storeId);

        if (result.Success)
        {
            TempData["Success"] = $"Period reopened successfully.";
        }
        else
        {
            TempData["Error"] = result.ErrorMessage;
        }

        return RedirectToAction(nameof(FiscalPeriodIndex));
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LockPeriod(int periodId)
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "1");
        var result = await _fiscalPeriodService.LockPeriodAsync(periodId, userId);

        if (result.Success)
        {
            TempData["Success"] = $"Period locked permanently.";
        }
        else
        {
            TempData["Error"] = result.ErrorMessage;
        }

        return RedirectToAction(nameof(FiscalPeriodIndex));
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateFiscalYear(int year)
    {
        try
        {
            var startDate = new DateTime(year, 1, 1);
            var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "1");

            var fiscalYear = await _fiscalPeriodService.CreateFiscalYearAsync(startDate, userId);
            TempData["Success"] = $"Fiscal year {fiscalYear.YearCode} created with 12 monthly periods.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(FiscalPeriodIndex));
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CloseFiscalYear(int fiscalYearId)
    {
        var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "1");
        var result = await _fiscalPeriodService.CloseFiscalYearAsync(fiscalYearId, userId);

        if (result.Success)
        {
            TempData["Success"] = "Fiscal year closed successfully.";
        }
        else
        {
            TempData["Error"] = result.ErrorMessage;
        }

        return RedirectToAction(nameof(FiscalPeriodIndex));
    }
}
