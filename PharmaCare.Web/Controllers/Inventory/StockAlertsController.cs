using Microsoft.AspNetCore.Mvc;
using PharmaCare.Application.Interfaces.Inventory;

namespace PharmaCare.Web.Controllers.Inventory;

public class StockAlertsController(IStockAlertService _alertService) : BaseController
{
    public async Task<IActionResult> StockAlertsIndex(string? type = null, string? severity = null)
    {
        var alerts = string.IsNullOrEmpty(type)
            ? await _alertService.GetActiveAlerts()
            : await _alertService.GetAlertsByType(type);

        // Filter by severity if specified
        if (!string.IsNullOrEmpty(severity))
        {
            alerts = alerts.Where(a => a.Severity == severity).ToList();
        }

        ViewBag.AlertType = type;
        ViewBag.Severity = severity;
        return View(alerts);
    }

    [HttpPost]
    public async Task<IActionResult> GenerateAlerts()
    {
        await _alertService.GenerateLowStockAlerts();
        await _alertService.GenerateExpiringStockAlerts();
        ShowMessage(MessageBox.Success, "Alerts generated successfully.");
        return RedirectToAction(nameof(StockAlertsIndex));
    }

    [HttpPost]
    public async Task<IActionResult> ResolveAlert(int id)
    {
        var success = await _alertService.ResolveAlert(id, LoginUserID);
        if (success)
        {
            ShowMessage(MessageBox.Success, "Alert resolved.");
        }
        return RedirectToAction(nameof(StockAlertsIndex));
    }

    [HttpGet]
    public async Task<JsonResult> GetAlertCount()
    {
        var count = await _alertService.GetActiveAlertCount();
        return Json(count);
    }
}
