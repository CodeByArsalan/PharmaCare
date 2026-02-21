using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCare.Application.Interfaces.Reports;
using PharmaCare.Application.ViewModels.Report;

namespace PharmaCare.Web.Controllers.Report;

[Authorize]
public class ReportController : BaseController
{
    private readonly ISalesReportService _salesReportService;
    private readonly IPurchaseReportService _purchaseReportService;
    private readonly IInventoryReportService _inventoryReportService;
    private readonly IFinancialReportService _financialReportService;

    public ReportController(
        ISalesReportService salesReportService,
        IPurchaseReportService purchaseReportService,
        IInventoryReportService inventoryReportService,
        IFinancialReportService financialReportService)
    {
        _salesReportService = salesReportService;
        _purchaseReportService = purchaseReportService;
        _inventoryReportService = inventoryReportService;
        _financialReportService = financialReportService;
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
        var vm = await _salesReportService.GetDailySalesSummaryAsync(reportDate);
        return View(vm);
    }

    public async Task<IActionResult> SalesReport(DateRangeFilter? filter)
    {
        filter ??= new DateRangeFilter();
        var vm = await _salesReportService.GetSalesReportAsync(filter);
        return View(vm);
    }

    public async Task<IActionResult> SalesByProduct(DateRangeFilter? filter)
    {
        filter ??= new DateRangeFilter();
        var vm = await _salesReportService.GetSalesByProductAsync(filter);
        return View(vm);
    }

    public async Task<IActionResult> SalesByCustomer(DateRangeFilter? filter)
    {
        filter ??= new DateRangeFilter();
        var vm = await _salesReportService.GetSalesByCustomerAsync(filter);
        return View(vm);
    }

    // ===================================================================
    //  PURCHASE REPORTS
    // ===================================================================

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

    // ===================================================================
    //  INVENTORY REPORTS
    // ===================================================================

    public async Task<IActionResult> CurrentStock(DateRangeFilter? filter)
    {
        filter ??= new DateRangeFilter();
        var vm = await _inventoryReportService.GetCurrentStockReportAsync(filter);
        return View(vm);
    }

    public async Task<IActionResult> LowStock(DateRangeFilter? filter)
    {
        filter ??= new DateRangeFilter();
        var vm = await _inventoryReportService.GetLowStockReportAsync(filter);
        return View(vm);
    }

    public async Task<IActionResult> ProductMovement(DateRangeFilter? filter)
    {
        filter ??= new DateRangeFilter();
        var vm = await _inventoryReportService.GetProductMovementReportAsync(filter);
        return View(vm);
    }

    public async Task<IActionResult> DeadStock(DateRangeFilter? filter)
    {
        filter ??= new DateRangeFilter { ThresholdDays = 30 };
        var vm = await _inventoryReportService.GetDeadStockReportAsync(filter);
        return View(vm);
    }

    // ===================================================================
    //  FINANCIAL REPORTS
    // ===================================================================

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

    // ===================================================================
    //  PARTY REPORTS
    // ===================================================================

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

    public async Task<IActionResult> CustomerBalanceSummary(DateTime? asOfDate)
    {
        var date = asOfDate ?? DateTime.Today;
        var vm = await _salesReportService.GetCustomerBalanceSummaryAsync(date);
        return View(vm);
    }
}
