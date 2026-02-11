using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.ViewModels.Report; // Updated namespace
using PharmaCare.Domain.Entities.Accounting;    // Ensure entities are available
using PharmaCare.Domain.Entities.Transactions;  
using PharmaCare.Domain.Entities.Configuration;
using PharmaCare.Domain.Entities.Finance;
using PharmaCare.Infrastructure; // For DBContext access

namespace PharmaCare.Infrastructure.Implementations; // Changed from Application.Implementations

/// <summary>
/// Read-only reporting service. All methods query existing data via EF Core — no mutations.
/// </summary>
public class ReportService : IReportService
{
    private readonly PharmaCareDBContext _db;

    // Transaction-type codes used throughout
    private static readonly string[] SaleCodes = { "SALE" };
    private static readonly string[] SaleReturnCodes = { "SRTN" };
    private static readonly string[] PurchaseCodes = { "GRN" };
    private static readonly string[] PurchaseReturnCodes = { "PRTN" };

    public ReportService(PharmaCareDBContext db)
    {
        _db = db;
    }

    // ========================================================================
    //  1.  SALES REPORTS
    // ========================================================================

    public async Task<DailySalesSummaryVM> GetDailySalesSummaryAsync(DateTime date)
    {
        var dayStart = date.Date;
        var dayEnd = dayStart.AddDays(1);

        var sales = await _db.StockMains
            .Include(s => s.TransactionType)
            .Include(s => s.StockDetails)
            .Where(s => s.TransactionDate >= dayStart && s.TransactionDate < dayEnd
                        && s.Status != "Void")
            .ToListAsync();

        var saleTransactions = sales.Where(s => SaleCodes.Contains(s.TransactionType!.Code)).ToList();
        var returnTransactions = sales.Where(s => SaleReturnCodes.Contains(s.TransactionType!.Code)).ToList();

        var totalSales = saleTransactions.Sum(s => s.TotalAmount);
        var totalReturns = returnTransactions.Sum(s => s.TotalAmount);
        var totalDiscounts = saleTransactions.Sum(s => s.DiscountAmount) + returnTransactions.Sum(s => s.DiscountAmount);
        var totalCOGS = saleTransactions.SelectMany(s => s.StockDetails).Sum(d => d.LineCost);
        var cashCollected = saleTransactions.Sum(s => s.PaidAmount);
        var outstanding = saleTransactions.Sum(s => s.BalanceAmount);

        // Hourly breakdown
        var hourly = saleTransactions
            .GroupBy(s => s.TransactionDate.Hour)
            .Select(g => new HourlySalesData
            {
                Hour = g.Key,
                Amount = g.Sum(s => s.TotalAmount),
                Count = g.Count()
            })
            .OrderBy(h => h.Hour)
            .ToList();

        return new DailySalesSummaryVM
        {
            Date = date,
            TotalSales = totalSales,
            TotalReturns = totalReturns,
            NetSales = totalSales - totalReturns,
            TotalDiscounts = totalDiscounts,
            TotalCOGS = totalCOGS,
            GrossProfit = (totalSales - totalReturns) - totalCOGS,
            CashCollected = cashCollected,
            OutstandingBalance = outstanding,
            TransactionCount = saleTransactions.Count,
            ItemsSold = (int)saleTransactions.SelectMany(s => s.StockDetails).Sum(d => d.Quantity),
            HourlySales = hourly
        };
    }

    public async Task<SalesReportVM> GetSalesReportAsync(DateRangeFilter filter)
    {
        var query = _db.StockMains
            .Include(s => s.TransactionType)
            .Include(s => s.Party)
            .Where(s => SaleCodes.Contains(s.TransactionType!.Code)
                        && s.TransactionDate >= filter.FromDate
                        && s.TransactionDate < filter.ToDate.AddDays(1)
                        && s.Status != "Void");

        if (filter.PartyId.HasValue)
            query = query.Where(s => s.Party_ID == filter.PartyId.Value);

        var data = await query.OrderByDescending(s => s.TransactionDate).ToListAsync();

        var vm = new SalesReportVM
        {
            Filter = filter,
            Rows = data.Select(s => new SalesReportRow
            {
                StockMainId = s.StockMainID,
                TransactionNo = s.TransactionNo,
                TransactionDate = s.TransactionDate,
                CustomerName = s.Party?.Name ?? "Walk-in Customer",
                SubTotal = s.SubTotal,
                Discount = s.DiscountAmount,
                TotalAmount = s.TotalAmount,
                PaidAmount = s.PaidAmount,
                BalanceAmount = s.BalanceAmount,
                Status = s.Status
            }).ToList()
        };

        vm.GrandTotal = vm.Rows.Sum(r => r.TotalAmount);
        vm.GrandDiscount = vm.Rows.Sum(r => r.Discount);
        vm.GrandPaid = vm.Rows.Sum(r => r.PaidAmount);
        vm.GrandBalance = vm.Rows.Sum(r => r.BalanceAmount);
        return vm;
    }

    public async Task<SalesByProductVM> GetSalesByProductAsync(DateRangeFilter filter)
    {
        var query = _db.StockDetails
            .Include(d => d.StockMain).ThenInclude(s => s!.TransactionType)
            .Include(d => d.Product).ThenInclude(p => p!.Category)
            .Where(d => SaleCodes.Contains(d.StockMain!.TransactionType!.Code)
                        && d.StockMain.TransactionDate >= filter.FromDate
                        && d.StockMain.TransactionDate < filter.ToDate.AddDays(1)
                        && d.StockMain.Status != "Void");

        if (filter.CategoryId.HasValue)
            query = query.Where(d => d.Product!.Category_ID == filter.CategoryId.Value);

        var details = await query.ToListAsync();

        var rows = details
            .GroupBy(d => d.Product_ID)
            .Select(g =>
            {
                var first = g.First();
                var revenue = g.Sum(d => d.LineTotal);
                var cost = g.Sum(d => d.LineCost);
                var profit = revenue - cost;
                return new SalesByProductRow
                {
                    ProductId = first.Product_ID,
                    ProductName = first.Product?.Name ?? "",
                    CategoryName = first.Product?.Category?.Name ?? "",
                    QuantitySold = g.Sum(d => d.Quantity),
                    Revenue = revenue,
                    Cost = cost,
                    GrossProfit = profit,
                    ProfitMarginPercent = revenue == 0 ? 0 : Math.Round(profit / revenue * 100, 2)
                };
            })
            .OrderByDescending(r => r.Revenue)
            .ToList();

        return new SalesByProductVM
        {
            Filter = filter,
            Rows = rows,
            TotalRevenue = rows.Sum(r => r.Revenue),
            TotalCost = rows.Sum(r => r.Cost),
            TotalProfit = rows.Sum(r => r.GrossProfit)
        };
    }

    public async Task<SalesByCustomerVM> GetSalesByCustomerAsync(DateRangeFilter filter)
    {
        var sales = await _db.StockMains
            .Include(s => s.TransactionType)
            .Include(s => s.Party)
            .Where(s => SaleCodes.Contains(s.TransactionType!.Code)
                        && s.TransactionDate >= filter.FromDate
                        && s.TransactionDate < filter.ToDate.AddDays(1)
                        && s.Status != "Void"
                        && s.Party_ID != null)
            .ToListAsync();

        var rows = sales
            .GroupBy(s => s.Party_ID)
            .Select(g =>
            {
                var first = g.First();
                return new SalesByCustomerRow
                {
                    PartyId = first.Party_ID ?? 0,
                    CustomerName = first.Party?.Name ?? "Unknown",
                    PurchaseCount = g.Count(),
                    TotalPurchases = g.Sum(s => s.TotalAmount),
                    TotalPaid = g.Sum(s => s.PaidAmount),
                    BalanceDue = g.Sum(s => s.BalanceAmount),
                    LastPurchaseDate = g.Max(s => s.TransactionDate)
                };
            })
            .OrderByDescending(r => r.TotalPurchases)
            .ToList();

        return new SalesByCustomerVM
        {
            Filter = filter,
            Rows = rows,
            TotalSales = rows.Sum(r => r.TotalPurchases),
            TotalPaid = rows.Sum(r => r.TotalPaid),
            TotalBalance = rows.Sum(r => r.BalanceDue)
        };
    }

    // ========================================================================
    //  2.  PURCHASE REPORTS
    // ========================================================================

    public async Task<PurchaseReportVM> GetPurchaseReportAsync(DateRangeFilter filter)
    {
        var query = _db.StockMains
            .Include(s => s.TransactionType)
            .Include(s => s.Party)
            .Where(s => PurchaseCodes.Contains(s.TransactionType!.Code)
                        && s.TransactionDate >= filter.FromDate
                        && s.TransactionDate < filter.ToDate.AddDays(1)
                        && s.Status != "Void");

        if (filter.PartyId.HasValue)
            query = query.Where(s => s.Party_ID == filter.PartyId.Value);

        var data = await query.OrderByDescending(s => s.TransactionDate).ToListAsync();

        var vm = new PurchaseReportVM
        {
            Filter = filter,
            Rows = data.Select(s => new PurchaseReportRow
            {
                StockMainId = s.StockMainID,
                TransactionNo = s.TransactionNo,
                TransactionDate = s.TransactionDate,
                SupplierName = s.Party?.Name ?? "",
                SubTotal = s.SubTotal,
                Discount = s.DiscountAmount,
                TotalAmount = s.TotalAmount,
                PaidAmount = s.PaidAmount,
                BalanceAmount = s.BalanceAmount,
                Status = s.Status
            }).ToList()
        };

        vm.GrandTotal = vm.Rows.Sum(r => r.TotalAmount);
        vm.GrandDiscount = vm.Rows.Sum(r => r.Discount);
        vm.GrandPaid = vm.Rows.Sum(r => r.PaidAmount);
        vm.GrandBalance = vm.Rows.Sum(r => r.BalanceAmount);
        return vm;
    }

    public async Task<PurchaseBySupplierVM> GetPurchaseBySupplierAsync(DateRangeFilter filter)
    {
        var purchases = await _db.StockMains
            .Include(s => s.TransactionType)
            .Include(s => s.Party)
            .Where(s => PurchaseCodes.Contains(s.TransactionType!.Code)
                        && s.TransactionDate >= filter.FromDate
                        && s.TransactionDate < filter.ToDate.AddDays(1)
                        && s.Status != "Void"
                        && s.Party_ID != null)
            .ToListAsync();

        var rows = purchases
            .GroupBy(s => s.Party_ID)
            .Select(g =>
            {
                var first = g.First();
                return new PurchaseBySupplierRow
                {
                    PartyId = first.Party_ID ?? 0,
                    SupplierName = first.Party?.Name ?? "Unknown",
                    PurchaseCount = g.Count(),
                    TotalPurchases = g.Sum(s => s.TotalAmount),
                    TotalPaid = g.Sum(s => s.PaidAmount),
                    BalanceDue = g.Sum(s => s.BalanceAmount),
                    LastPurchaseDate = g.Max(s => s.TransactionDate)
                };
            })
            .OrderByDescending(r => r.TotalPurchases)
            .ToList();

        return new PurchaseBySupplierVM
        {
            Filter = filter,
            Rows = rows,
            TotalPurchases = rows.Sum(r => r.TotalPurchases),
            TotalPaid = rows.Sum(r => r.TotalPaid),
            TotalBalance = rows.Sum(r => r.BalanceDue)
        };
    }

    // ========================================================================
    //  3.  INVENTORY REPORTS
    // ========================================================================

    public async Task<CurrentStockReportVM> GetCurrentStockReportAsync(DateRangeFilter filter)
    {
        var products = await _db.Products
            .Include(p => p.Category)
            .Where(p => p.IsActive)
            .ToListAsync();

        if (filter.CategoryId.HasValue)
            products = products.Where(p => p.Category_ID == filter.CategoryId.Value).ToList();

        // Pre-load all stock details with transaction types
        var stockDetails = await _db.StockDetails
            .Include(d => d.StockMain).ThenInclude(s => s!.TransactionType)
            .Where(d => d.StockMain!.Status != "Void")
            .ToListAsync();

        var rows = new List<CurrentStockRow>();
        foreach (var p in products)
        {
            var pDetails = stockDetails.Where(d => d.Product_ID == p.ProductID).ToList();

            var purchasedQty = pDetails
                .Where(d => d.StockMain!.TransactionType!.StockDirection == 1)
                .Sum(d => d.Quantity);

            var soldQty = pDetails
                .Where(d => d.StockMain!.TransactionType!.StockDirection == -1
                            && SaleCodes.Contains(d.StockMain!.TransactionType!.Code))
                .Sum(d => d.Quantity);

            var returnedInQty = pDetails
                .Where(d => d.StockMain!.TransactionType!.StockDirection == 1
                            && SaleReturnCodes.Contains(d.StockMain!.TransactionType!.Code))
                .Sum(d => d.Quantity);

            var returnedOutQty = pDetails
                .Where(d => d.StockMain!.TransactionType!.StockDirection == -1
                            && PurchaseReturnCodes.Contains(d.StockMain!.TransactionType!.Code))
                .Sum(d => d.Quantity);

            var currentStock = p.OpeningQuantity + purchasedQty - soldQty + returnedInQty - returnedOutQty;

            rows.Add(new CurrentStockRow
            {
                ProductId = p.ProductID,
                ProductName = p.Name,
                CategoryName = p.Category?.Name ?? "",
                OpeningQty = p.OpeningQuantity,
                PurchasedQty = purchasedQty,
                SoldQty = soldQty,
                ReturnedInQty = returnedInQty,
                ReturnedOutQty = returnedOutQty,
                CurrentStock = currentStock,
                CostPrice = p.OpeningPrice,
                StockValue = currentStock * p.OpeningPrice,
                ReorderLevel = p.ReorderLevel,
                IsLowStock = currentStock <= p.ReorderLevel
            });
        }

        return new CurrentStockReportVM
        {
            Filter = filter,
            Rows = rows.OrderBy(r => r.CategoryName).ThenBy(r => r.ProductName).ToList(),
            TotalStockValue = rows.Sum(r => r.StockValue),
            TotalProducts = rows.Count,
            LowStockCount = rows.Count(r => r.IsLowStock && r.CurrentStock > 0),
            OutOfStockCount = rows.Count(r => r.CurrentStock <= 0)
        };
    }

    public async Task<LowStockReportVM> GetLowStockReportAsync(DateRangeFilter filter)
    {
        var stockReport = await GetCurrentStockReportAsync(filter);
        var lowItems = stockReport.Rows.Where(r => r.IsLowStock).ToList();

        var rows = lowItems.Select(r => new LowStockRow
        {
            ProductId = r.ProductId,
            ProductName = r.ProductName,
            CategoryName = r.CategoryName,
            CurrentStock = r.CurrentStock,
            ReorderLevel = r.ReorderLevel,
            Shortfall = r.ReorderLevel - r.CurrentStock,
            SuggestedReorderQty = Math.Max(0, (r.ReorderLevel * 2) - r.CurrentStock)
        }).OrderByDescending(r => r.Shortfall).ToList();

        return new LowStockReportVM
        {
            Filter = filter,
            Rows = rows,
            TotalAlerts = rows.Count,
            OutOfStockCount = rows.Count(r => r.CurrentStock <= 0)
        };
    }

    public async Task<ProductMovementReportVM> GetProductMovementReportAsync(DateRangeFilter filter)
    {
        if (!filter.ProductId.HasValue)
            return new ProductMovementReportVM { Filter = filter };

        var product = await _db.Products.FindAsync(filter.ProductId.Value);
        if (product == null)
            return new ProductMovementReportVM { Filter = filter };

        // Get all movements for this product within date range
        var details = await _db.StockDetails
            .Include(d => d.StockMain).ThenInclude(s => s!.TransactionType)
            .Where(d => d.Product_ID == filter.ProductId.Value
                        && d.StockMain!.Status != "Void"
                        && d.StockMain.TransactionDate >= filter.FromDate
                        && d.StockMain.TransactionDate < filter.ToDate.AddDays(1))
            .OrderBy(d => d.StockMain!.TransactionDate)
            .ThenBy(d => d.StockDetailID)
            .ToListAsync();

        // Calculate opening balance (movements before FromDate + opening qty)
        var priorDetails = await _db.StockDetails
            .Include(d => d.StockMain).ThenInclude(s => s!.TransactionType)
            .Where(d => d.Product_ID == filter.ProductId.Value
                        && d.StockMain!.Status != "Void"
                        && d.StockMain.TransactionDate < filter.FromDate)
            .ToListAsync();

        decimal openingBalance = product.OpeningQuantity;
        foreach (var d in priorDetails)
        {
            var dir = d.StockMain!.TransactionType!.StockDirection;
            openingBalance += d.Quantity * dir;
        }

        var runningBalance = openingBalance;
        var rows = new List<ProductMovementRow>();
        foreach (var d in details)
        {
            var dir = d.StockMain!.TransactionType!.StockDirection;
            var qtyIn = dir > 0 ? d.Quantity : 0;
            var qtyOut = dir < 0 ? d.Quantity : 0;
            runningBalance += d.Quantity * dir;

            rows.Add(new ProductMovementRow
            {
                TransactionDate = d.StockMain.TransactionDate,
                TransactionNo = d.StockMain.TransactionNo,
                TransactionType = d.StockMain.TransactionType!.Name,
                QtyIn = qtyIn,
                QtyOut = qtyOut,
                RunningBalance = runningBalance
            });
        }

        return new ProductMovementReportVM
        {
            Filter = filter,
            ProductName = product.Name,
            Rows = rows,
            OpeningBalance = openingBalance,
            ClosingBalance = runningBalance
        };
    }

    public async Task<DeadStockReportVM> GetDeadStockReportAsync(DateRangeFilter filter)
    {
        var thresholdDays = filter.ThresholdDays ?? 30;
        var cutoffDate = DateTime.Today.AddDays(-thresholdDays);

        // Get current stock first
        var stockReport = await GetCurrentStockReportAsync(filter);

        // Get last sale dates per product
        var lastSaleDates = await _db.StockDetails
            .Include(d => d.StockMain).ThenInclude(s => s!.TransactionType)
            .Where(d => SaleCodes.Contains(d.StockMain!.TransactionType!.Code)
                        && d.StockMain.Status != "Void")
            .GroupBy(d => d.Product_ID)
            .Select(g => new { ProductId = g.Key, LastDate = g.Max(d => d.StockMain!.TransactionDate) })
            .ToDictionaryAsync(x => x.ProductId, x => x.LastDate);

        var rows = stockReport.Rows
            .Where(r => r.CurrentStock > 0)
            .Select(r =>
            {
                var found = lastSaleDates.TryGetValue(r.ProductId, out var lastDate);
                var daysSince = found ? (int)(DateTime.Today - lastDate).TotalDays : 9999;
                return new DeadStockRow
                {
                    ProductId = r.ProductId,
                    ProductName = r.ProductName,
                    CategoryName = r.CategoryName,
                    CurrentStock = r.CurrentStock,
                    StockValue = r.StockValue,
                    LastSaleDate = found ? lastDate : null,
                    DaysSinceLastSale = daysSince
                };
            })
            .Where(r => !r.LastSaleDate.HasValue || r.LastSaleDate.Value < cutoffDate)
            .OrderByDescending(r => r.DaysSinceLastSale)
            .ToList();

        return new DeadStockReportVM
        {
            Filter = filter,
            Rows = rows,
            TotalDeadStockValue = rows.Sum(r => r.StockValue),
            TotalItems = rows.Count
        };
    }

    // ========================================================================
    //  4.  FINANCIAL REPORTS
    // ========================================================================

    public async Task<ProfitLossVM> GetProfitLossAsync(DateRangeFilter filter)
    {
        var fromDate = filter.FromDate;
        var toDate = filter.ToDate.AddDays(1);

        // Revenue
        var salesData = await _db.StockMains
            .Include(s => s.TransactionType)
            .Include(s => s.StockDetails)
            .Where(s => s.TransactionDate >= fromDate && s.TransactionDate < toDate
                        && s.Status != "Void"
                        && (SaleCodes.Contains(s.TransactionType!.Code) || SaleReturnCodes.Contains(s.TransactionType!.Code)))
            .ToListAsync();

        var totalRevenue = salesData.Where(s => SaleCodes.Contains(s.TransactionType!.Code)).Sum(s => s.TotalAmount);
        var salesReturns = salesData.Where(s => SaleReturnCodes.Contains(s.TransactionType!.Code)).Sum(s => s.TotalAmount);
        var netRevenue = totalRevenue - salesReturns;

        var cogs = salesData
            .Where(s => SaleCodes.Contains(s.TransactionType!.Code))
            .SelectMany(s => s.StockDetails)
            .Sum(d => d.LineCost);

        var grossProfit = netRevenue - cogs;

        // Expenses
        var expenses = await _db.Expenses
            .Include(e => e.ExpenseCategory)
            .Where(e => e.ExpenseDate >= fromDate && e.ExpenseDate < toDate)
            .ToListAsync();

        var expensesByCategory = expenses
            .GroupBy(e => e.ExpenseCategory?.Name ?? "Uncategorized")
            .Select(g => new ExpenseCategoryTotal
            {
                CategoryName = g.Key,
                Amount = g.Sum(e => e.Amount)
            })
            .OrderByDescending(e => e.Amount)
            .ToList();

        var totalExpenses = expenses.Sum(e => e.Amount);

        return new ProfitLossVM
        {
            Filter = filter,
            TotalRevenue = totalRevenue,
            SalesReturns = salesReturns,
            NetRevenue = netRevenue,
            COGS = cogs,
            GrossProfit = grossProfit,
            GrossProfitMargin = netRevenue == 0 ? 0 : Math.Round(grossProfit / netRevenue * 100, 2),
            ExpensesByCategory = expensesByCategory,
            TotalExpenses = totalExpenses,
            NetProfit = grossProfit - totalExpenses,
            NetProfitMargin = netRevenue == 0 ? 0 : Math.Round((grossProfit - totalExpenses) / netRevenue * 100, 2)
        };
    }

    public async Task<CashFlowReportVM> GetCashFlowReportAsync(DateRangeFilter filter)
    {
        var fromDate = filter.FromDate;
        var toDate = filter.ToDate.AddDays(1);

        // Cash In: Paid amount from sales
        var salesReceipts = await _db.StockMains
            .Include(s => s.TransactionType)
            .Where(s => SaleCodes.Contains(s.TransactionType!.Code)
                        && s.TransactionDate >= fromDate && s.TransactionDate < toDate
                        && s.Status != "Void")
            .SumAsync(s => s.PaidAmount);

        // Cash In: Customer payments
        var customerPayments = await _db.Payments
            .Where(p => p.PaymentType == "RECEIPT"
                        && p.PaymentDate >= fromDate && p.PaymentDate < toDate)
            .SumAsync(p => p.Amount);

        // Cash Out: Paid amount on purchases
        var purchasePayments = await _db.StockMains
            .Include(s => s.TransactionType)
            .Where(s => PurchaseCodes.Contains(s.TransactionType!.Code)
                        && s.TransactionDate >= fromDate && s.TransactionDate < toDate
                        && s.Status != "Void")
            .SumAsync(s => s.PaidAmount);

        // Cash Out: Supplier payments
        var supplierPayments = await _db.Payments
            .Where(p => p.PaymentType == "PAYMENT"
                        && p.PaymentDate >= fromDate && p.PaymentDate < toDate)
            .SumAsync(p => p.Amount);

        // Cash Out: Expenses
        var expensePayments = await _db.Expenses
            .Where(e => e.ExpenseDate >= fromDate && e.ExpenseDate < toDate)
            .SumAsync(e => e.Amount);

        var totalCashIn = salesReceipts + customerPayments;
        var totalCashOut = purchasePayments + supplierPayments + expensePayments;

        // Daily data for chart
        var allDays = Enumerable.Range(0, (int)(filter.ToDate - filter.FromDate).TotalDays + 1)
            .Select(i => filter.FromDate.AddDays(i))
            .ToList();

        var dailyData = new List<DailyCashFlowData>();
        foreach (var day in allDays)
        {
            var dayEnd = day.AddDays(1);

            var dayIn = await _db.StockMains
                .Include(s => s.TransactionType)
                .Where(s => SaleCodes.Contains(s.TransactionType!.Code)
                            && s.TransactionDate >= day && s.TransactionDate < dayEnd
                            && s.Status != "Void")
                .SumAsync(s => s.PaidAmount)
                + await _db.Payments
                    .Where(p => p.PaymentType == "RECEIPT" && p.PaymentDate >= day && p.PaymentDate < dayEnd)
                    .SumAsync(p => p.Amount);

            var dayOut = await _db.StockMains
                .Include(s => s.TransactionType)
                .Where(s => PurchaseCodes.Contains(s.TransactionType!.Code)
                            && s.TransactionDate >= day && s.TransactionDate < dayEnd
                            && s.Status != "Void")
                .SumAsync(s => s.PaidAmount)
                + await _db.Payments
                    .Where(p => p.PaymentType == "PAYMENT" && p.PaymentDate >= day && p.PaymentDate < dayEnd)
                    .SumAsync(p => p.Amount)
                + await _db.Expenses
                    .Where(e => e.ExpenseDate >= day && e.ExpenseDate < dayEnd)
                    .SumAsync(e => e.Amount);

            dailyData.Add(new DailyCashFlowData
            {
                Date = day,
                CashIn = dayIn,
                CashOut = dayOut,
                NetFlow = dayIn - dayOut
            });
        }

        return new CashFlowReportVM
        {
            Filter = filter,
            SalesReceipts = salesReceipts,
            CustomerPayments = customerPayments,
            TotalCashIn = totalCashIn,
            PurchasePayments = purchasePayments,
            SupplierPayments = supplierPayments,
            ExpensePayments = expensePayments,
            TotalCashOut = totalCashOut,
            ClosingBalance = totalCashIn - totalCashOut,
            DailyData = dailyData
        };
    }

    public async Task<ReceivablesAgingVM> GetReceivablesAgingAsync(DateTime asOfDate)
    {
        // Outstanding sale transactions
        var sales = await _db.StockMains
            .Include(s => s.TransactionType)
            .Include(s => s.Party)
            .Where(s => SaleCodes.Contains(s.TransactionType!.Code)
                        && s.TransactionDate <= asOfDate
                        && s.BalanceAmount > 0
                        && s.Status != "Void"
                        && s.Party_ID != null)
            .ToListAsync();

        var rows = sales
            .GroupBy(s => s.Party_ID)
            .Select(g =>
            {
                var row = new AgingRow
                {
                    PartyId = g.Key ?? 0,
                    PartyName = g.First().Party?.Name ?? "Unknown"
                };

                foreach (var s in g)
                {
                    var daysOld = (int)(asOfDate - s.TransactionDate).TotalDays;
                    if (daysOld <= 30) row.Current += s.BalanceAmount;
                    else if (daysOld <= 60) row.Days31_60 += s.BalanceAmount;
                    else if (daysOld <= 90) row.Days61_90 += s.BalanceAmount;
                    else row.Days90Plus += s.BalanceAmount;
                }

                row.Total = row.Current + row.Days31_60 + row.Days61_90 + row.Days90Plus;
                return row;
            })
            .OrderByDescending(r => r.Total)
            .ToList();

        return new ReceivablesAgingVM
        {
            AsOfDate = asOfDate,
            Rows = rows,
            TotalCurrent = rows.Sum(r => r.Current),
            Total31_60 = rows.Sum(r => r.Days31_60),
            Total61_90 = rows.Sum(r => r.Days61_90),
            Total90Plus = rows.Sum(r => r.Days90Plus),
            GrandTotal = rows.Sum(r => r.Total)
        };
    }

    public async Task<PayablesAgingVM> GetPayablesAgingAsync(DateTime asOfDate)
    {
        var purchases = await _db.StockMains
            .Include(s => s.TransactionType)
            .Include(s => s.Party)
            .Where(s => PurchaseCodes.Contains(s.TransactionType!.Code)
                        && s.TransactionDate <= asOfDate
                        && s.BalanceAmount > 0
                        && s.Status != "Void"
                        && s.Party_ID != null)
            .ToListAsync();

        var rows = purchases
            .GroupBy(s => s.Party_ID)
            .Select(g =>
            {
                var row = new AgingRow
                {
                    PartyId = g.Key ?? 0,
                    PartyName = g.First().Party?.Name ?? "Unknown"
                };

                foreach (var s in g)
                {
                    var daysOld = (int)(asOfDate - s.TransactionDate).TotalDays;
                    if (daysOld <= 30) row.Current += s.BalanceAmount;
                    else if (daysOld <= 60) row.Days31_60 += s.BalanceAmount;
                    else if (daysOld <= 90) row.Days61_90 += s.BalanceAmount;
                    else row.Days90Plus += s.BalanceAmount;
                }

                row.Total = row.Current + row.Days31_60 + row.Days61_90 + row.Days90Plus;
                return row;
            })
            .OrderByDescending(r => r.Total)
            .ToList();

        return new PayablesAgingVM
        {
            AsOfDate = asOfDate,
            Rows = rows,
            TotalCurrent = rows.Sum(r => r.Current),
            Total31_60 = rows.Sum(r => r.Days31_60),
            Total61_90 = rows.Sum(r => r.Days61_90),
            Total90Plus = rows.Sum(r => r.Days90Plus),
            GrandTotal = rows.Sum(r => r.Total)
        };
    }

    public async Task<ExpenseReportVM> GetExpenseReportAsync(DateRangeFilter filter)
    {
        var query = _db.Expenses
            .Include(e => e.ExpenseCategory)
            .Include(e => e.SourceAccount)
            .Where(e => e.ExpenseDate >= filter.FromDate
                        && e.ExpenseDate < filter.ToDate.AddDays(1));

        if (filter.ExpenseCategoryId.HasValue)
            query = query.Where(e => e.ExpenseCategory_ID == filter.ExpenseCategoryId.Value);

        var expenses = await query.OrderByDescending(e => e.ExpenseDate).ToListAsync();

        var rows = expenses.Select(e => new ExpenseReportRow
        {
            ExpenseId = e.ExpenseID,
            ExpenseDate = e.ExpenseDate,
            CategoryName = e.ExpenseCategory?.Name ?? "",
            Description = e.Description ?? "",
            Amount = e.Amount,
            SourceAccountName = e.SourceAccount?.Name ?? "",
            Reference = e.Reference
        }).ToList();

        var categoryTotals = rows
            .GroupBy(r => r.CategoryName)
            .Select(g => new ExpenseCategoryTotal
            {
                CategoryName = g.Key,
                Amount = g.Sum(r => r.Amount)
            })
            .OrderByDescending(c => c.Amount)
            .ToList();

        return new ExpenseReportVM
        {
            Filter = filter,
            Rows = rows,
            CategoryTotals = categoryTotals,
            GrandTotal = rows.Sum(r => r.Amount)
        };
    }

    public async Task<TrialBalanceVM> GetTrialBalanceAsync(DateTime asOfDate)
    {
        var voucherDetails = await _db.VoucherDetails
            .Include(vd => vd.Voucher)
            .Include(vd => vd.Account).ThenInclude(a => a!.AccountHead)
            .Include(vd => vd.Account).ThenInclude(a => a!.AccountType)
            .Where(vd => vd.Voucher!.VoucherDate <= asOfDate
                         && vd.Voucher.Status == "Posted")
            .ToListAsync();

        var rows = voucherDetails
            .GroupBy(vd => vd.Account_ID)
            .Select(g =>
            {
                var first = g.First();
                var debitTotal = g.Sum(vd => vd.DebitAmount);
                var creditTotal = g.Sum(vd => vd.CreditAmount);
                var balance = debitTotal - creditTotal;
                return new TrialBalanceRow
                {
                    AccountId = first.Account_ID,
                    AccountName = first.Account?.Name ?? "",
                    AccountHeadName = first.Account?.AccountHead?.HeadName ?? "",
                    AccountTypeName = first.Account?.AccountType?.Name ?? "",
                    DebitTotal = debitTotal,
                    CreditTotal = creditTotal,
                    Balance = Math.Abs(balance),
                    BalanceType = balance >= 0 ? "Dr" : "Cr"
                };
            })
            .Where(r => r.DebitTotal != 0 || r.CreditTotal != 0)
            .OrderBy(r => r.AccountHeadName).ThenBy(r => r.AccountName)
            .ToList();

        var totalDebit = rows.Sum(r => r.DebitTotal);
        var totalCredit = rows.Sum(r => r.CreditTotal);

        return new TrialBalanceVM
        {
            AsOfDate = asOfDate,
            Rows = rows,
            TotalDebit = totalDebit,
            TotalCredit = totalCredit,
            IsBalanced = totalDebit == totalCredit
        };
    }

    public async Task<GeneralLedgerVM> GetGeneralLedgerAsync(DateRangeFilter filter)
    {
        if (!filter.AccountId.HasValue)
            return new GeneralLedgerVM { Filter = filter };

        var account = await _db.Accounts.FindAsync(filter.AccountId.Value);
        if (account == null)
            return new GeneralLedgerVM { Filter = filter };

        // Opening balance (all voucher details before FromDate)
        var priorEntries = await _db.VoucherDetails
            .Include(vd => vd.Voucher)
            .Where(vd => vd.Account_ID == filter.AccountId.Value
                         && vd.Voucher!.VoucherDate < filter.FromDate
                         && vd.Voucher.Status == "Posted")
            .ToListAsync();

        decimal openingBalance = priorEntries.Sum(vd => vd.DebitAmount - vd.CreditAmount);

        // Period entries
        var entries = await _db.VoucherDetails
            .Include(vd => vd.Voucher)
            .Where(vd => vd.Account_ID == filter.AccountId.Value
                         && vd.Voucher!.VoucherDate >= filter.FromDate
                         && vd.Voucher.VoucherDate < filter.ToDate.AddDays(1)
                         && vd.Voucher.Status == "Posted")
            .OrderBy(vd => vd.Voucher!.VoucherDate)
            .ThenBy(vd => vd.VoucherDetailID)
            .ToListAsync();

        var runningBalance = openingBalance;
        var rows = entries.Select(vd =>
        {
            runningBalance += vd.DebitAmount - vd.CreditAmount;
            return new GeneralLedgerRow
            {
                Date = vd.Voucher!.VoucherDate,
                VoucherNo = vd.Voucher.VoucherNo,
                Narration = vd.Description ?? vd.Voucher.Narration ?? "",
                Debit = vd.DebitAmount,
                Credit = vd.CreditAmount,
                RunningBalance = runningBalance
            };
        }).ToList();

        return new GeneralLedgerVM
        {
            Filter = filter,
            AccountName = account.Name,
            Rows = rows,
            OpeningBalance = openingBalance,
            TotalDebit = rows.Sum(r => r.Debit),
            TotalCredit = rows.Sum(r => r.Credit),
            ClosingBalance = runningBalance
        };
    }

    // ========================================================================
    //  5.  PARTY REPORTS
    // ========================================================================

    public async Task<PartyLedgerVM> GetPartyLedgerAsync(DateRangeFilter filter, string partyType)
    {
        if (!filter.PartyId.HasValue)
            return new PartyLedgerVM { Filter = filter, PartyType = partyType };

        var party = await _db.Parties.FindAsync(filter.PartyId.Value);
        if (party == null)
            return new PartyLedgerVM { Filter = filter, PartyType = partyType };

        var isCustomer = partyType == "Customer";
        var relevantSaleCodes = isCustomer ? SaleCodes : PurchaseCodes;
        var relevantReturnCodes = isCustomer ? SaleReturnCodes : PurchaseReturnCodes;
        var paymentType = isCustomer ? "RECEIPT" : "PAYMENT";

        // Opening balance: transactions before FromDate
        var priorTransactions = await _db.StockMains
            .Include(s => s.TransactionType)
            .Where(s => s.Party_ID == filter.PartyId.Value
                        && s.TransactionDate < filter.FromDate
                        && s.Status != "Void"
                        && (relevantSaleCodes.Contains(s.TransactionType!.Code) || relevantReturnCodes.Contains(s.TransactionType!.Code)))
            .ToListAsync();

        var priorPayments = await _db.Payments
            .Where(p => p.Party_ID == filter.PartyId.Value
                        && p.PaymentDate < filter.FromDate
                        && p.PaymentType == paymentType)
            .ToListAsync();

        // For customer: Sale = Debit (adds to balance), Receipt = Credit (reduces balance)
        // For supplier: Purchase = Credit (adds to payable), Payment = Debit (reduces payable)
        decimal openingBalance = party.OpeningBalance;
        if (isCustomer)
        {
            openingBalance += priorTransactions.Where(s => SaleCodes.Contains(s.TransactionType!.Code)).Sum(s => s.TotalAmount);
            openingBalance -= priorTransactions.Where(s => SaleReturnCodes.Contains(s.TransactionType!.Code)).Sum(s => s.TotalAmount);
            openingBalance -= priorPayments.Sum(p => p.Amount);
        }
        else
        {
            openingBalance += priorTransactions.Where(s => PurchaseCodes.Contains(s.TransactionType!.Code)).Sum(s => s.TotalAmount);
            openingBalance -= priorTransactions.Where(s => PurchaseReturnCodes.Contains(s.TransactionType!.Code)).Sum(s => s.TotalAmount);
            openingBalance -= priorPayments.Sum(p => p.Amount);
        }

        // Period transactions
        var periodTransactions = await _db.StockMains
            .Include(s => s.TransactionType)
            .Where(s => s.Party_ID == filter.PartyId.Value
                        && s.TransactionDate >= filter.FromDate
                        && s.TransactionDate < filter.ToDate.AddDays(1)
                        && s.Status != "Void"
                        && (relevantSaleCodes.Contains(s.TransactionType!.Code) || relevantReturnCodes.Contains(s.TransactionType!.Code)))
            .ToListAsync();

        var periodPayments = await _db.Payments
            .Where(p => p.Party_ID == filter.PartyId.Value
                        && p.PaymentDate >= filter.FromDate
                        && p.PaymentDate < filter.ToDate.AddDays(1)
                        && p.PaymentType == paymentType)
            .ToListAsync();

        // Merge and sort by date
        var ledgerEntries = new List<(DateTime Date, string Ref, string Desc, decimal Debit, decimal Credit)>();

        foreach (var s in periodTransactions)
        {
            if (isCustomer)
            {
                if (SaleCodes.Contains(s.TransactionType!.Code))
                    ledgerEntries.Add((s.TransactionDate, s.TransactionNo, "Sale", s.TotalAmount, 0));
                else
                    ledgerEntries.Add((s.TransactionDate, s.TransactionNo, "Sale Return", 0, s.TotalAmount));
            }
            else
            {
                if (PurchaseCodes.Contains(s.TransactionType!.Code))
                    ledgerEntries.Add((s.TransactionDate, s.TransactionNo, "Purchase", s.TotalAmount, 0));
                else
                    ledgerEntries.Add((s.TransactionDate, s.TransactionNo, "Purchase Return", 0, s.TotalAmount));
            }
        }

        foreach (var p in periodPayments)
        {
            if (isCustomer)
                ledgerEntries.Add((p.PaymentDate, p.Reference ?? $"PMT-{p.PaymentID}", "Payment Received", 0, p.Amount));
            else
                ledgerEntries.Add((p.PaymentDate, p.Reference ?? $"PMT-{p.PaymentID}", "Payment Made", p.Amount, 0));
        }

        ledgerEntries = ledgerEntries.OrderBy(e => e.Date).ToList();

        var runningBalance = openingBalance;
        var rows = ledgerEntries.Select(e =>
        {
            runningBalance += e.Debit - e.Credit;
            return new PartyLedgerRow
            {
                Date = e.Date,
                Reference = e.Ref,
                Description = e.Desc,
                Debit = e.Debit,
                Credit = e.Credit,
                RunningBalance = runningBalance
            };
        }).ToList();

        return new PartyLedgerVM
        {
            Filter = filter,
            PartyName = party.Name,
            PartyType = partyType,
            Rows = rows,
            OpeningBalance = openingBalance,
            TotalDebit = rows.Sum(r => r.Debit),
            TotalCredit = rows.Sum(r => r.Credit),
            ClosingBalance = runningBalance
        };
    }

    public async Task<CustomerBalanceSummaryVM> GetCustomerBalanceSummaryAsync(DateTime asOfDate)
    {
        var customers = await _db.Parties
            .Where(p => p.PartyType == "Customer" && p.IsActive)
            .ToListAsync();

        var sales = await _db.StockMains
            .Include(s => s.TransactionType)
            .Where(s => SaleCodes.Contains(s.TransactionType!.Code)
                        && s.TransactionDate <= asOfDate
                        && s.Status != "Void"
                        && s.Party_ID != null)
            .ToListAsync();

        var receipts = await _db.Payments
            .Where(p => p.PaymentType == "RECEIPT" && p.PaymentDate <= asOfDate)
            .ToListAsync();

        var rows = customers.Select(c =>
        {
            var totalSales = sales.Where(s => s.Party_ID == c.PartyID).Sum(s => s.TotalAmount);
            var totalReceipts = receipts.Where(r => r.Party_ID == c.PartyID).Sum(r => r.Amount);
            // Factor in paid amount at time of sale
            var paidAtSale = sales.Where(s => s.Party_ID == c.PartyID).Sum(s => s.PaidAmount);
            var balance = totalSales - paidAtSale - totalReceipts + c.OpeningBalance;

            return new CustomerBalanceRow
            {
                PartyId = c.PartyID,
                CustomerName = c.Name,
                TotalSales = totalSales,
                TotalReceipts = paidAtSale + totalReceipts,
                BalanceDue = balance,
                CreditLimit = c.CreditLimit,
                IsOverLimit = c.CreditLimit > 0 && balance > c.CreditLimit
            };
        })
        .Where(r => r.TotalSales > 0 || r.BalanceDue != 0)
        .OrderByDescending(r => r.BalanceDue)
        .ToList();

        return new CustomerBalanceSummaryVM
        {
            AsOfDate = asOfDate,
            Rows = rows,
            TotalSales = rows.Sum(r => r.TotalSales),
            TotalReceipts = rows.Sum(r => r.TotalReceipts),
            TotalBalance = rows.Sum(r => r.BalanceDue)
        };
    }
}
