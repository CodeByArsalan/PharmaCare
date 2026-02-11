using System.ComponentModel.DataAnnotations;

namespace PharmaCare.Application.ViewModels.Report;

// ===================== SHARED / FILTER MODELS =====================

/// <summary>
/// Common date-range filter used by most reports.
/// </summary>
public class DateRangeFilter
{
    [Display(Name = "From Date")]
    [DataType(DataType.Date)]
    public DateTime FromDate { get; set; } = DateTime.Today.AddMonths(-1);

    [Display(Name = "To Date")]
    [DataType(DataType.Date)]
    public DateTime ToDate { get; set; } = DateTime.Today;

    public int? CategoryId { get; set; }
    public int? SubCategoryId { get; set; }
    public int? PartyId { get; set; }
    public int? ProductId { get; set; }
    public int? AccountId { get; set; }
    public int? ExpenseCategoryId { get; set; }
    public int? ThresholdDays { get; set; }
}

// ===================== 1. SALES REPORTS =====================

/// <summary>
/// 1.1 Daily Sales Summary
/// </summary>
public class DailySalesSummaryVM
{
    public DateTime Date { get; set; } = DateTime.Today;
    public decimal TotalSales { get; set; }
    public decimal TotalReturns { get; set; }
    public decimal NetSales { get; set; }
    public decimal TotalDiscounts { get; set; }
    public decimal TotalCOGS { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal CashCollected { get; set; }
    public decimal OutstandingBalance { get; set; }
    public int TransactionCount { get; set; }
    public int ItemsSold { get; set; }
    // Chart data
    public List<HourlySalesData> HourlySales { get; set; } = new();
}

public class HourlySalesData
{
    public int Hour { get; set; }
    public decimal Amount { get; set; }
    public int Count { get; set; }
}

/// <summary>
/// 1.2 Sales Report (Date Range)
/// </summary>
public class SalesReportVM
{
    public DateRangeFilter Filter { get; set; } = new();
    public List<SalesReportRow> Rows { get; set; } = new();
    public decimal GrandTotal { get; set; }
    public decimal GrandDiscount { get; set; }
    public decimal GrandPaid { get; set; }
    public decimal GrandBalance { get; set; }
}

public class SalesReportRow
{
    public int StockMainId { get; set; }
    public string TransactionNo { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public string CustomerName { get; set; } = "Walk-in Customer";
    public decimal SubTotal { get; set; }
    public decimal Discount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal BalanceAmount { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// 1.3 Sales by Product
/// </summary>
public class SalesByProductVM
{
    public DateRangeFilter Filter { get; set; } = new();
    public List<SalesByProductRow> Rows { get; set; } = new();
    public decimal TotalRevenue { get; set; }
    public decimal TotalCost { get; set; }
    public decimal TotalProfit { get; set; }
}

public class SalesByProductRow
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public decimal QuantitySold { get; set; }
    public decimal Revenue { get; set; }
    public decimal Cost { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal ProfitMarginPercent { get; set; }
}

/// <summary>
/// 1.4 Sales by Customer
/// </summary>
public class SalesByCustomerVM
{
    public DateRangeFilter Filter { get; set; } = new();
    public List<SalesByCustomerRow> Rows { get; set; } = new();
    public decimal TotalSales { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalBalance { get; set; }
}

public class SalesByCustomerRow
{
    public int PartyId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int PurchaseCount { get; set; }
    public decimal TotalPurchases { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal BalanceDue { get; set; }
    public DateTime? LastPurchaseDate { get; set; }
}

// ===================== 2. PURCHASE REPORTS =====================

/// <summary>
/// 2.1 Purchase Report (Date Range)
/// </summary>
public class PurchaseReportVM
{
    public DateRangeFilter Filter { get; set; } = new();
    public List<PurchaseReportRow> Rows { get; set; } = new();
    public decimal GrandTotal { get; set; }
    public decimal GrandDiscount { get; set; }
    public decimal GrandPaid { get; set; }
    public decimal GrandBalance { get; set; }
}

public class PurchaseReportRow
{
    public int StockMainId { get; set; }
    public string TransactionNo { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public decimal SubTotal { get; set; }
    public decimal Discount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal BalanceAmount { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// 2.2 Purchase by Supplier
/// </summary>
public class PurchaseBySupplierVM
{
    public DateRangeFilter Filter { get; set; } = new();
    public List<PurchaseBySupplierRow> Rows { get; set; } = new();
    public decimal TotalPurchases { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalBalance { get; set; }
}

public class PurchaseBySupplierRow
{
    public int PartyId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public int PurchaseCount { get; set; }
    public decimal TotalPurchases { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal BalanceDue { get; set; }
    public DateTime? LastPurchaseDate { get; set; }
}

// ===================== 3. INVENTORY REPORTS =====================

/// <summary>
/// 3.1 Current Stock Report
/// </summary>
public class CurrentStockReportVM
{
    public DateRangeFilter Filter { get; set; } = new();
    public List<CurrentStockRow> Rows { get; set; } = new();
    public decimal TotalStockValue { get; set; }
    public int TotalProducts { get; set; }
    public int LowStockCount { get; set; }
    public int OutOfStockCount { get; set; }
}

public class CurrentStockRow
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public decimal OpeningQty { get; set; }
    public decimal PurchasedQty { get; set; }
    public decimal SoldQty { get; set; }
    public decimal ReturnedInQty { get; set; }
    public decimal ReturnedOutQty { get; set; }
    public decimal CurrentStock { get; set; }
    public decimal CostPrice { get; set; }
    public decimal StockValue { get; set; }
    public int ReorderLevel { get; set; }
    public bool IsLowStock { get; set; }
}

/// <summary>
/// 3.2 Low Stock / Reorder Alert
/// </summary>
public class LowStockReportVM
{
    public DateRangeFilter Filter { get; set; } = new();
    public List<LowStockRow> Rows { get; set; } = new();
    public int TotalAlerts { get; set; }
    public int OutOfStockCount { get; set; }
}

public class LowStockRow
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public decimal CurrentStock { get; set; }
    public int ReorderLevel { get; set; }
    public decimal Shortfall { get; set; }
    public decimal SuggestedReorderQty { get; set; }
}

/// <summary>
/// 3.3 Product Movement Report
/// </summary>
public class ProductMovementReportVM
{
    public DateRangeFilter Filter { get; set; } = new();
    public string ProductName { get; set; } = string.Empty;
    public List<ProductMovementRow> Rows { get; set; } = new();
    public decimal OpeningBalance { get; set; }
    public decimal ClosingBalance { get; set; }
}

public class ProductMovementRow
{
    public DateTime TransactionDate { get; set; }
    public string TransactionNo { get; set; } = string.Empty;
    public string TransactionType { get; set; } = string.Empty;
    public decimal QtyIn { get; set; }
    public decimal QtyOut { get; set; }
    public decimal RunningBalance { get; set; }
}

/// <summary>
/// 3.4 Dead / Slow-Moving Stock
/// </summary>
public class DeadStockReportVM
{
    public DateRangeFilter Filter { get; set; } = new();
    public List<DeadStockRow> Rows { get; set; } = new();
    public decimal TotalDeadStockValue { get; set; }
    public int TotalItems { get; set; }
}

public class DeadStockRow
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public decimal CurrentStock { get; set; }
    public decimal StockValue { get; set; }
    public DateTime? LastSaleDate { get; set; }
    public int DaysSinceLastSale { get; set; }
}

// ===================== 4. FINANCIAL REPORTS =====================

/// <summary>
/// 4.1 Profit & Loss Statement
/// </summary>
public class ProfitLossVM
{
    public DateRangeFilter Filter { get; set; } = new();
    public decimal TotalRevenue { get; set; }
    public decimal SalesReturns { get; set; }
    public decimal NetRevenue { get; set; }
    public decimal COGS { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal GrossProfitMargin { get; set; }
    public List<ExpenseCategoryTotal> ExpensesByCategory { get; set; } = new();
    public decimal TotalExpenses { get; set; }
    public decimal NetProfit { get; set; }
    public decimal NetProfitMargin { get; set; }
}

public class ExpenseCategoryTotal
{
    public string CategoryName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

/// <summary>
/// 4.2 Cash Flow Report
/// </summary>
public class CashFlowReportVM
{
    public DateRangeFilter Filter { get; set; } = new();
    public decimal OpeningBalance { get; set; }
    public decimal SalesReceipts { get; set; }
    public decimal CustomerPayments { get; set; }
    public decimal TotalCashIn { get; set; }
    public decimal PurchasePayments { get; set; }
    public decimal SupplierPayments { get; set; }
    public decimal ExpensePayments { get; set; }
    public decimal TotalCashOut { get; set; }
    public decimal ClosingBalance { get; set; }
    // Daily chart data
    public List<DailyCashFlowData> DailyData { get; set; } = new();
}

public class DailyCashFlowData
{
    public DateTime Date { get; set; }
    public decimal CashIn { get; set; }
    public decimal CashOut { get; set; }
    public decimal NetFlow { get; set; }
}

/// <summary>
/// 4.3 Receivables Aging Report
/// </summary>
public class ReceivablesAgingVM
{
    public DateTime AsOfDate { get; set; } = DateTime.Today;
    public List<AgingRow> Rows { get; set; } = new();
    public decimal TotalCurrent { get; set; }
    public decimal Total31_60 { get; set; }
    public decimal Total61_90 { get; set; }
    public decimal Total90Plus { get; set; }
    public decimal GrandTotal { get; set; }
}

/// <summary>
/// 4.4 Payables Aging Report
/// </summary>
public class PayablesAgingVM
{
    public DateTime AsOfDate { get; set; } = DateTime.Today;
    public List<AgingRow> Rows { get; set; } = new();
    public decimal TotalCurrent { get; set; }
    public decimal Total31_60 { get; set; }
    public decimal Total61_90 { get; set; }
    public decimal Total90Plus { get; set; }
    public decimal GrandTotal { get; set; }
}

public class AgingRow
{
    public int PartyId { get; set; }
    public string PartyName { get; set; } = string.Empty;
    public decimal Current { get; set; }      // 0-30 days
    public decimal Days31_60 { get; set; }
    public decimal Days61_90 { get; set; }
    public decimal Days90Plus { get; set; }
    public decimal Total { get; set; }
}

/// <summary>
/// 4.5 Expense Report
/// </summary>
public class ExpenseReportVM
{
    public DateRangeFilter Filter { get; set; } = new();
    public List<ExpenseReportRow> Rows { get; set; } = new();
    public List<ExpenseCategoryTotal> CategoryTotals { get; set; } = new();
    public decimal GrandTotal { get; set; }
}

public class ExpenseReportRow
{
    public int ExpenseId { get; set; }
    public DateTime ExpenseDate { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string SourceAccountName { get; set; } = string.Empty;
    public string? Reference { get; set; }
}

/// <summary>
/// 4.6 Trial Balance
/// </summary>
public class TrialBalanceVM
{
    public DateTime AsOfDate { get; set; } = DateTime.Today;
    public List<TrialBalanceRow> Rows { get; set; } = new();
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public bool IsBalanced { get; set; }
}

public class TrialBalanceRow
{
    public int AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string AccountHeadName { get; set; } = string.Empty;
    public string AccountTypeName { get; set; } = string.Empty;
    public decimal DebitTotal { get; set; }
    public decimal CreditTotal { get; set; }
    public decimal Balance { get; set; }
    public string BalanceType { get; set; } = string.Empty; // "Dr" or "Cr"
}

/// <summary>
/// 4.7 General Ledger
/// </summary>
public class GeneralLedgerVM
{
    public DateRangeFilter Filter { get; set; } = new();
    public string AccountName { get; set; } = string.Empty;
    public List<GeneralLedgerRow> Rows { get; set; } = new();
    public decimal OpeningBalance { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public decimal ClosingBalance { get; set; }
}

public class GeneralLedgerRow
{
    public DateTime Date { get; set; }
    public string VoucherNo { get; set; } = string.Empty;
    public string Narration { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal RunningBalance { get; set; }
}

// ===================== 5. PARTY REPORTS =====================

/// <summary>
/// 5.1 Customer Ledger / 5.2 Supplier Ledger (shared model)
/// </summary>
public class PartyLedgerVM
{
    public DateRangeFilter Filter { get; set; } = new();
    public string PartyName { get; set; } = string.Empty;
    public string PartyType { get; set; } = string.Empty;
    public List<PartyLedgerRow> Rows { get; set; } = new();
    public decimal OpeningBalance { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public decimal ClosingBalance { get; set; }
}

public class PartyLedgerRow
{
    public DateTime Date { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal RunningBalance { get; set; }
}

/// <summary>
/// 5.3 Customer Balance Summary
/// </summary>
public class CustomerBalanceSummaryVM
{
    public DateTime AsOfDate { get; set; } = DateTime.Today;
    public List<CustomerBalanceRow> Rows { get; set; } = new();
    public decimal TotalSales { get; set; }
    public decimal TotalReceipts { get; set; }
    public decimal TotalBalance { get; set; }
}

public class CustomerBalanceRow
{
    public int PartyId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public decimal TotalSales { get; set; }
    public decimal TotalReceipts { get; set; }
    public decimal BalanceDue { get; set; }
    public decimal CreditLimit { get; set; }
    public bool IsOverLimit { get; set; }
}
