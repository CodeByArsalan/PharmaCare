using Microsoft.AspNetCore.Mvc;
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Domain.Entities.Accounting;
using System.Security.Claims;
using PharmaCare.Web.Filters;

namespace PharmaCare.Web.Controllers.Accounting
{
    public class FinancialPeriodController : BaseController
    {
        private readonly IFinancialPeriodService _financialPeriodService;

        public FinancialPeriodController(IFinancialPeriodService financialPeriodService)
        {
            _financialPeriodService = financialPeriodService;
        }

        public async Task<IActionResult> Index()
        {
            var periods = await _financialPeriodService.GetAllAsync();
            return View(periods);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(FinancialPeriod period)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                await _financialPeriodService.CreateAsync(period, userId);
                return Json(new { success = true, message = "Financial Period created successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = SafeErrorMessage(ex, "Creating financial period") });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [LinkedToPage("FinancialPeriod", "Index", PermissionType = "edit")]
        public async Task<IActionResult> Close(int id, string? remarks)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var success = await _financialPeriodService.ClosePeriodAsync(id, remarks, userId);
                if (success)
                    return Json(new { success = true, message = "Financial Period closed successfully." });
                else
                    return Json(new { success = false, message = "Failed to close financial period." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = SafeErrorMessage(ex, $"Closing financial period {id}") });
            }
        }

        // Re-opening a CLOSED period is the single control that lets anyone post into books that
        // were already signed off, so it is gated on the strongest permission the page has.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [LinkedToPage("FinancialPeriod", "Index", PermissionType = "delete")]
        public async Task<IActionResult> Open(int id)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var success = await _financialPeriodService.OpenPeriodAsync(id, userId);
                if (success)
                    return Json(new { success = true, message = "Financial Period opened successfully." });
                else
                    return Json(new { success = false, message = "Failed to open financial period." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = SafeErrorMessage(ex, $"Opening financial period {id}") });
            }
        }
    }
}
