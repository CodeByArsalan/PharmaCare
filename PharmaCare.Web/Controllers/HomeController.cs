using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCare.Application.Interfaces.SaleManagement;
using PharmaCare.Application.Interfaces.Inventory;
using PharmaCare.Web.Models;
using PharmaCare.Infrastructure.Interfaces;

namespace PharmaCare.Web.Controllers;

public class HomeController(IPosService _posService, IStockService _stockService, IComboBoxRepository _comboBox) : BaseController
{
    public async Task<IActionResult> Index(int? storeId)
    {
        // 0. Prepare Store Selector
        var stores = await _comboBox.GetStoresByLoginUserIDAsync(LoginUserID);
        ViewBag.Stores = stores;
        ViewBag.SelectedStoreId = storeId;
        ViewBag.SelectedStoreName = storeId.HasValue
            ? stores.FirstOrDefault(s => s.Value == storeId.ToString())?.Text ?? "Unknown Store"
            : "All Stores";

        ViewBag.LoginUserRole = LoginUserTypeID;

        // 2. Get Today's Sales (Filtered)
        var today = DateTime.Today;
        var salesHistory = await _posService.GetSalesHistory(today, today, storeId);
        var totalSalesToday = salesHistory.Sum(s => s.TotalAmount);
        var transactionsToday = salesHistory.Count;

        // 3. Populate ViewModel
        ViewBag.TotalCustomers = 0; // Customer count no longer tracked - Party is used
        ViewBag.TotalSalesToday = totalSalesToday;
        ViewBag.TransactionsToday = transactionsToday;

        // 4. Low Stock Items (Filtered)
        ViewBag.LowStockItems = await _stockService.GetLowStockItemsCount(storeId);

        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
