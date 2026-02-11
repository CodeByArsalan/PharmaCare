using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Application.ViewModels.Report;

namespace PharmaCare.Web.Controllers;

[Authorize]
public class ReportController : BaseController
{
    private readonly IReportService _reportService;
    private readonly IPartyService _partyService;
    private readonly IProductService _productService;
    private readonly ICategoryService _categoryService;
    private readonly IAccountService _accountService;

    public ReportController(
        IReportService reportService,
        IPartyService partyService,
        IProductService productService,
        ICategoryService categoryService,
        IAccountService accountService)
    {
        _reportService = reportService;
        _partyService = partyService;
        _productService = productService;
        _categoryService = categoryService;
        _accountService = accountService;
    }

    /// <summary>
    /// Reports Dashboard / Index page.
    /// </summary>
    public IActionResult Index()
    {
        return View();
    }

    // ===================================================================
    //  SALES REPORTS
    // ===================================================================

    public async Task<IActionResult> DailySalesSummary(DateTime? date)
    {
        var reportDate = date ?? DateTime.Today;
        var vm = await _reportService.GetDailySalesSummaryAsync(reportDate);
        return View(vm);
    }

    public async Task<IActionResult> SalesReport(DateRangeFilter? filter)
    {
        filter ??= new DateRangeFilter();
        await LoadCustomerDropdownAsync();
        var vm = await _reportService.GetSalesReportAsync(filter);
        return View(vm);
    }

    public async Task<IActionResult> SalesByProduct(DateRangeFilter? filter)
    {
        filter ??= new DateRangeFilter();
        await LoadCategoryDropdownAsync();
        var vm = await _reportService.GetSalesByProductAsync(filter);
        return View(vm);
    }

    public async Task<IActionResult> SalesByCustomer(DateRangeFilter? filter)
    {
        filter ??= new DateRangeFilter();
        var vm = await _reportService.GetSalesByCustomerAsync(filter);
        return View(vm);
    }

    // ===================================================================
    //  PURCHASE REPORTS
    // ===================================================================

    public async Task<IActionResult> PurchaseReport(DateRangeFilter? filter)
    {
        filter ??= new DateRangeFilter();
        await LoadSupplierDropdownAsync();
        var vm = await _reportService.GetPurchaseReportAsync(filter);
        return View(vm);
    }

    public async Task<IActionResult> PurchaseBySupplier(DateRangeFilter? filter)
    {
        filter ??= new DateRangeFilter();
        var vm = await _reportService.GetPurchaseBySupplierAsync(filter);
        return View(vm);
    }

    // ===================================================================
    //  INVENTORY REPORTS
    // ===================================================================

    public async Task<IActionResult> CurrentStock(DateRangeFilter? filter)
    {
        filter ??= new DateRangeFilter();
        await LoadCategoryDropdownAsync();
        var vm = await _reportService.GetCurrentStockReportAsync(filter);
        return View(vm);
    }

    public async Task<IActionResult> LowStock(DateRangeFilter? filter)
    {
        filter ??= new DateRangeFilter();
        await LoadCategoryDropdownAsync();
        var vm = await _reportService.GetLowStockReportAsync(filter);
        return View(vm);
    }

    public async Task<IActionResult> ProductMovement(DateRangeFilter? filter)
    {
        filter ??= new DateRangeFilter();
        await LoadProductDropdownAsync();
        var vm = await _reportService.GetProductMovementReportAsync(filter);
        return View(vm);
    }

    public async Task<IActionResult> DeadStock(DateRangeFilter? filter)
    {
        filter ??= new DateRangeFilter { ThresholdDays = 30 };
        await LoadCategoryDropdownAsync();
        var vm = await _reportService.GetDeadStockReportAsync(filter);
        return View(vm);
    }

    // ===================================================================
    //  FINANCIAL REPORTS
    // ===================================================================

    public async Task<IActionResult> ProfitLoss(DateRangeFilter? filter)
    {
        filter ??= new DateRangeFilter();
        var vm = await _reportService.GetProfitLossAsync(filter);
        return View(vm);
    }

    public async Task<IActionResult> CashFlow(DateRangeFilter? filter)
    {
        filter ??= new DateRangeFilter();
        var vm = await _reportService.GetCashFlowReportAsync(filter);
        return View(vm);
    }

    public async Task<IActionResult> ReceivablesAging(DateTime? asOfDate)
    {
        var date = asOfDate ?? DateTime.Today;
        var vm = await _reportService.GetReceivablesAgingAsync(date);
        return View(vm);
    }

    public async Task<IActionResult> PayablesAging(DateTime? asOfDate)
    {
        var date = asOfDate ?? DateTime.Today;
        var vm = await _reportService.GetPayablesAgingAsync(date);
        return View(vm);
    }

    public async Task<IActionResult> ExpenseReport(DateRangeFilter? filter)
    {
        filter ??= new DateRangeFilter();
        var vm = await _reportService.GetExpenseReportAsync(filter);
        return View(vm);
    }

    public async Task<IActionResult> TrialBalance(DateTime? asOfDate)
    {
        var date = asOfDate ?? DateTime.Today;
        var vm = await _reportService.GetTrialBalanceAsync(date);
        return View(vm);
    }

    public async Task<IActionResult> GeneralLedger(DateRangeFilter? filter)
    {
        filter ??= new DateRangeFilter();
        await LoadAccountDropdownAsync();
        var vm = await _reportService.GetGeneralLedgerAsync(filter);
        return View(vm);
    }

    // ===================================================================
    //  PARTY REPORTS
    // ===================================================================

    public async Task<IActionResult> CustomerLedger(DateRangeFilter? filter)
    {
        filter ??= new DateRangeFilter();
        await LoadCustomerDropdownAsync();
        var vm = await _reportService.GetPartyLedgerAsync(filter, "Customer");
        return View(vm);
    }

    public async Task<IActionResult> SupplierLedger(DateRangeFilter? filter)
    {
        filter ??= new DateRangeFilter();
        await LoadSupplierDropdownAsync();
        var vm = await _reportService.GetPartyLedgerAsync(filter, "Supplier");
        return View(vm);
    }

    public async Task<IActionResult> CustomerBalanceSummary(DateTime? asOfDate)
    {
        var date = asOfDate ?? DateTime.Today;
        var vm = await _reportService.GetCustomerBalanceSummaryAsync(date);
        return View(vm);
    }

    // ===================================================================
    //  DROPDOWN HELPERS
    // ===================================================================

    private async Task LoadCustomerDropdownAsync()
    {
        var parties = await _partyService.GetAllAsync();
        ViewBag.Customers = new SelectList(
            parties.Where(p => p.IsActive && p.PartyType == "Customer"),
            "PartyID", "Name");
    }

    private async Task LoadSupplierDropdownAsync()
    {
        var parties = await _partyService.GetAllAsync();
        ViewBag.Suppliers = new SelectList(
            parties.Where(p => p.IsActive && p.PartyType == "Supplier"),
            "PartyID", "Name");
    }

    private async Task LoadProductDropdownAsync()
    {
        var products = await _productService.GetAllAsync();
        ViewBag.Products = new SelectList(
            products.Where(p => p.IsActive),
            "ProductID", "Name");
    }

    private async Task LoadCategoryDropdownAsync()
    {
        var categories = await _categoryService.GetAllAsync();
        ViewBag.Categories = new SelectList(
            categories.Where(c => c.IsActive),
            "CategoryID", "Name");
    }

    private async Task LoadAccountDropdownAsync()
    {
        var accounts = await _accountService.GetAllAsync();
        ViewBag.Accounts = new SelectList(
            accounts.Where(a => a.IsActive),
            "AccountID", "Name");
    }
}
