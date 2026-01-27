using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCare.Application.Interfaces.Reports;
using PharmaCare.Web.Utilities;

using PharmaCare.Application.Interfaces.Membership;
using PharmaCare.Domain.ViewModels;
using PharmaCare.Infrastructure.Interfaces;
using PharmaCare.Application.Interfaces.Configuration;
using Microsoft.EntityFrameworkCore;
using PharmaCare.Domain.Models.Configuration;

namespace PharmaCare.Web.Controllers.Reports;

public class ReportsController(IReportService _reportService, ISystemUserService _systemUserService, IStoreService _storeService) : BaseController
{
    private async Task PrepareStoreSelection(int? selectedStoreId)
    {
        ViewBag.Stores = await _storeService.GetStoresByLoginUserID(LoginUserID);
        ViewBag.SelectedStoreId = selectedStoreId;
    }
    public IActionResult ReportsIndex()
    {
        var allowedReports = new List<string>();

        // Admins (UserTypeID == 1) usually have access to everything, but if we strictly follow assignments:
        // Let's stick to assignments as per request.
        var userWithPages = _systemUserService.GetUserWithPages(LoginUserID);

        if (userWithPages != null)
        {
            void Traverse(List<MenuItemDto> items)
            {
                foreach (var item in items)
                {
                    if (string.Equals(item.ControllerName, "Reports", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(item.ViewName))
                    {
                        allowedReports.Add(item.ViewName);
                    }
                    if (item.Children != null && item.Children.Any())
                    {
                        Traverse(item.Children);
                    }
                }
            }
            Traverse(userWithPages.MenuItems);
        }

        // TEMPORARY: Whitelist new available reports until permissions are configured in DB
        allowedReports.AddRange(new[]
        {
            "SlowMovingReport",
            "PurchaseReport",
            "ProfitLossReport",
            "CustomerAnalyticsReport",
            "ExpiryWastageReport"
        });

        ViewBag.AllowedReports = allowedReports;
        return View();
    }

    public async Task<IActionResult> SalesReport(DateTime? startDate, DateTime? endDate, string? storeId)
    {
        int? decryptedStoreId = null;
        if (!string.IsNullOrEmpty(storeId))
        {
            decryptedStoreId = DecryptId(storeId);
        }

        // Default to current month if no dates provided
        if (!startDate.HasValue || !endDate.HasValue)
        {
            startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            endDate = DateTime.Now;
        }

        var report = await _reportService.GetSalesReport(startDate.Value, endDate.Value, decryptedStoreId);

        ViewBag.StartDate = startDate.Value.ToString("yyyy-MM-dd");
        ViewBag.EndDate = endDate.Value.ToString("yyyy-MM-dd");
        await PrepareStoreSelection(decryptedStoreId);

        return View(report);
    }

    public async Task<IActionResult> InventoryReport(string? storeId)
    {
        int? decryptedStoreId = null;
        if (!string.IsNullOrEmpty(storeId))
        {
            decryptedStoreId = DecryptId(storeId);
        }

        var report = await _reportService.GetInventoryReport(decryptedStoreId);
        await PrepareStoreSelection(decryptedStoreId);
        return View(report);
    }

    public async Task<IActionResult> SalesDetailReport(DateTime? startDate, DateTime? endDate, string? storeId)
    {
        int? decryptedStoreId = null;
        if (!string.IsNullOrEmpty(storeId))
        {
            decryptedStoreId = DecryptId(storeId);
        }

        // Default to current month
        if (!startDate.HasValue || !endDate.HasValue)
        {
            startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            endDate = DateTime.Now;
        }

        var report = await _reportService.GetSalesDetailReport(startDate.Value, endDate.Value, decryptedStoreId);

        ViewBag.StartDate = startDate.Value.ToString("yyyy-MM-dd");
        ViewBag.EndDate = endDate.Value.ToString("yyyy-MM-dd");
        await PrepareStoreSelection(decryptedStoreId);

        return View(report);
    }

    public async Task<IActionResult> StockMovementReport(DateTime? startDate, DateTime? endDate, string? productName, string? storeId)
    {
        int? decryptedStoreId = null;
        if (!string.IsNullOrEmpty(storeId))
        {
            decryptedStoreId = DecryptId(storeId);
        }

        // Default to last 30 days
        if (!startDate.HasValue || !endDate.HasValue)
        {
            endDate = DateTime.Now;
            startDate = DateTime.Now.AddDays(-30);
        }

        var report = await _reportService.GetStockMovementReport(startDate.Value, endDate.Value, productName, decryptedStoreId);

        ViewBag.StartDate = startDate.Value.ToString("yyyy-MM-dd");
        ViewBag.EndDate = endDate.Value.ToString("yyyy-MM-dd");
        ViewBag.ProductName = productName;
        await PrepareStoreSelection(decryptedStoreId);

        return View(report);
    }

    public async Task<IActionResult> SlowMovingReport(int daysThreshold = 30, string? storeId = null)
    {
        int? decryptedStoreId = null;
        if (!string.IsNullOrEmpty(storeId))
        {
            decryptedStoreId = DecryptId(storeId);
        }

        var report = await _reportService.GetSlowMovingItemsReport(daysThreshold, decryptedStoreId);
        ViewBag.DaysThreshold = daysThreshold;
        await PrepareStoreSelection(decryptedStoreId);
        return View(report);
    }

    public async Task<IActionResult> PurchaseReport(DateTime? startDate, DateTime? endDate, string? storeId)
    {
        int? decryptedStoreId = null;
        if (!string.IsNullOrEmpty(storeId))
        {
            decryptedStoreId = DecryptId(storeId);
        }

        if (!startDate.HasValue || !endDate.HasValue)
        {
            startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            endDate = DateTime.Now;
        }

        var report = await _reportService.GetPurchaseReport(startDate.Value, endDate.Value, decryptedStoreId);

        ViewBag.StartDate = startDate.Value.ToString("yyyy-MM-dd");
        ViewBag.EndDate = endDate.Value.ToString("yyyy-MM-dd");
        await PrepareStoreSelection(decryptedStoreId);

        return View(report);
    }

    public async Task<IActionResult> ProfitLossReport(DateTime? startDate, DateTime? endDate, string? storeId)
    {
        int? decryptedStoreId = null;
        if (!string.IsNullOrEmpty(storeId))
        {
            decryptedStoreId = DecryptId(storeId);
        }

        if (!startDate.HasValue || !endDate.HasValue)
        {
            startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            endDate = DateTime.Now;
        }

        var report = await _reportService.GetProfitLossReport(startDate.Value, endDate.Value, decryptedStoreId);

        ViewBag.StartDate = startDate.Value.ToString("yyyy-MM-dd");
        ViewBag.EndDate = endDate.Value.ToString("yyyy-MM-dd");
        await PrepareStoreSelection(decryptedStoreId);

        return View(report);
    }

    public async Task<IActionResult> CustomerAnalyticsReport(DateTime? startDate, DateTime? endDate, string? storeId)
    {
        int? decryptedStoreId = null;
        if (!string.IsNullOrEmpty(storeId))
        {
            decryptedStoreId = DecryptId(storeId);
        }

        if (!startDate.HasValue || !endDate.HasValue)
        {
            startDate = new DateTime(DateTime.Now.Year, 1, 1); // Year to date
            endDate = DateTime.Now;
        }

        var report = await _reportService.GetCustomerAnalyticsReport(startDate.Value, endDate.Value, decryptedStoreId);

        ViewBag.StartDate = startDate.Value.ToString("yyyy-MM-dd");
        ViewBag.EndDate = endDate.Value.ToString("yyyy-MM-dd");
        await PrepareStoreSelection(decryptedStoreId);

        return View(report);
    }

    public async Task<IActionResult> ExpiryWastageReport(string? storeId)
    {
        int? decryptedStoreId = null;
        if (!string.IsNullOrEmpty(storeId))
        {
            decryptedStoreId = DecryptId(storeId);
        }

        var report = await _reportService.GetExpiryWastageReport(decryptedStoreId);
        await PrepareStoreSelection(decryptedStoreId);
        return View(report);
    }
}
