using PharmaCare.Application.ViewModels.Report;

namespace PharmaCare.Application.Interfaces;

/// <summary>
/// Service interface for generating all application reports.
/// This is a read-only service — no data modifications.
/// </summary>
public interface IReportService
{
    // ===== SALES REPORTS =====
    Task<DailySalesSummaryVM> GetDailySalesSummaryAsync(DateTime date);
    Task<SalesReportVM> GetSalesReportAsync(DateRangeFilter filter);
    Task<SalesByProductVM> GetSalesByProductAsync(DateRangeFilter filter);
    Task<SalesByCustomerVM> GetSalesByCustomerAsync(DateRangeFilter filter);

    // ===== PURCHASE REPORTS =====
    Task<PurchaseReportVM> GetPurchaseReportAsync(DateRangeFilter filter);
    Task<PurchaseBySupplierVM> GetPurchaseBySupplierAsync(DateRangeFilter filter);

    // ===== INVENTORY REPORTS =====
    Task<CurrentStockReportVM> GetCurrentStockReportAsync(DateRangeFilter filter);
    Task<LowStockReportVM> GetLowStockReportAsync(DateRangeFilter filter);
    Task<ProductMovementReportVM> GetProductMovementReportAsync(DateRangeFilter filter);
    Task<DeadStockReportVM> GetDeadStockReportAsync(DateRangeFilter filter);

    // ===== FINANCIAL REPORTS =====
    Task<ProfitLossVM> GetProfitLossAsync(DateRangeFilter filter);
    Task<CashFlowReportVM> GetCashFlowReportAsync(DateRangeFilter filter);
    Task<ReceivablesAgingVM> GetReceivablesAgingAsync(DateTime asOfDate);
    Task<PayablesAgingVM> GetPayablesAgingAsync(DateTime asOfDate);
    Task<ExpenseReportVM> GetExpenseReportAsync(DateRangeFilter filter);
    Task<TrialBalanceVM> GetTrialBalanceAsync(DateTime asOfDate);
    Task<GeneralLedgerVM> GetGeneralLedgerAsync(DateRangeFilter filter);

    // ===== PARTY REPORTS =====
    Task<PartyLedgerVM> GetPartyLedgerAsync(DateRangeFilter filter, string partyType);
    Task<CustomerBalanceSummaryVM> GetCustomerBalanceSummaryAsync(DateTime asOfDate);
}
