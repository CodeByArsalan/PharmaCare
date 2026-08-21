using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces.Reports;
using PharmaCare.Application.ViewModels.Report;
using PharmaCare.Domain.Enums;
using PharmaCare.Infrastructure;

namespace PharmaCare.Infrastructure.Implementations.Reports;

public class SalesReportService : ISalesReportService
{
    private readonly PharmaCareDBContext _db;
    private static readonly string[] SaleCodes = { "SALE" };
    private static readonly string[] SaleReturnCodes = { "SRTN" };

    public SalesReportService(PharmaCareDBContext db)
    {
        _db = db;
    }

    public async Task<DailySalesSummaryVM> GetDailySalesSummaryAsync(DateTime date)
    {
        var dayStart = date.Date;
        var dayEnd = dayStart.AddDays(1);

        var dailyTotalsByType = await _db.StockMains
            .AsNoTracking()
            .Where(s => s.TransactionDate >= dayStart
                        && s.TransactionDate < dayEnd
                        && s.Status != "Void"
                        && (SaleCodes.Contains(s.TransactionType!.Code) || SaleReturnCodes.Contains(s.TransactionType!.Code)))
            .GroupBy(s => s.TransactionType!.Code)
            .Select(g => new
            {
                Code = g.Key,
                TotalAmount = g.Sum(s => s.TotalAmount),
                TotalDiscount = g.Sum(s => s.DiscountAmount),
                TotalBalance = g.Sum(s => s.BalanceAmount),
                Count = g.Count()
            })
            .ToListAsync();

        var totalSales = dailyTotalsByType
            .Where(x => SaleCodes.Contains(x.Code))
            .Sum(x => x.TotalAmount);

        var totalReturns = dailyTotalsByType
            .Where(x => SaleReturnCodes.Contains(x.Code))
            .Sum(x => x.TotalAmount);

        var totalDiscounts = dailyTotalsByType.Sum(x => x.TotalDiscount);

        // Cash actually received during the day, from normalized Payment events (the canonical
        // source for collections). Invoice PaidAmount is invoice-dated and rewritten by later
        // receipts, so summing it misses cash received today against older invoices, retroactively
        // changes past days' summaries, and counts non-cash credit-note applications as cash.
        // Receipts count in, customer refunds count out; Adjustment-method rows are bookkeeping,
        // not cash (same convention as the cash flow report).
        var customerPaymentTotals = await _db.Payments
            .AsNoTracking()
            .Where(p => p.PaymentDate >= dayStart
                        && p.PaymentDate < dayEnd
                        && !p.IsVoided
                        && p.PaymentMethod.ToUpper() != PaymentMethod.Adjustment.ToString().ToUpper()
                        && p.Party!.PartyType == "Customer"
                        && (p.PaymentType == PaymentType.RECEIPT.ToString()
                            || p.PaymentType == PaymentType.REFUND.ToString()))
            .GroupBy(p => p.PaymentType)
            .Select(g => new { Type = g.Key, Amount = g.Sum(p => p.Amount) })
            .ToListAsync();

        var cashCollected = customerPaymentTotals.Where(x => x.Type == PaymentType.RECEIPT.ToString()).Sum(x => x.Amount)
                            - customerPaymentTotals.Where(x => x.Type == PaymentType.REFUND.ToString()).Sum(x => x.Amount);

        var outstanding = dailyTotalsByType
            .Where(x => SaleCodes.Contains(x.Code))
            .Sum(x => x.TotalBalance);

        var transactionCount = dailyTotalsByType
            .Where(x => SaleCodes.Contains(x.Code))
            .Sum(x => x.Count);

        // Netted like revenue: a return brings its cost back, so its LineCost subtracts.
        var cogsByType = await _db.StockDetails
            .AsNoTracking()
            .Where(d => d.StockMain!.TransactionDate >= dayStart
                        && d.StockMain.TransactionDate < dayEnd
                        && d.StockMain.Status != "Void"
                        && (SaleCodes.Contains(d.StockMain.TransactionType!.Code)
                            || SaleReturnCodes.Contains(d.StockMain.TransactionType!.Code)))
            .GroupBy(d => d.StockMain!.TransactionType!.Code)
            .Select(g => new { Code = g.Key, Amount = g.Sum(d => d.LineCost) })
            .ToListAsync();

        var totalCOGS = cogsByType.Where(x => SaleCodes.Contains(x.Code)).Sum(x => x.Amount)
                        - cogsByType.Where(x => SaleReturnCodes.Contains(x.Code)).Sum(x => x.Amount);

        var itemsSold = await _db.StockDetails
            .AsNoTracking()
            .Where(d => d.StockMain!.TransactionDate >= dayStart
                        && d.StockMain.TransactionDate < dayEnd
                        && d.StockMain.Status != "Void"
                        && SaleCodes.Contains(d.StockMain.TransactionType!.Code))
            .SumAsync(d => (decimal?)d.Quantity) ?? 0;

        var hourly = await _db.StockMains
            .AsNoTracking()
            .Where(s => s.TransactionDate >= dayStart
                        && s.TransactionDate < dayEnd
                        && s.Status != "Void"
                        && SaleCodes.Contains(s.TransactionType!.Code))
            .GroupBy(s => s.TransactionDate.Hour)
            .Select(g => new HourlySalesData
            {
                Hour = g.Key,
                Amount = g.Sum(s => s.TotalAmount),
                Count = g.Count()
            })
            .OrderBy(h => h.Hour)
            .ToListAsync();

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
            TransactionCount = transactionCount,
            ItemsSold = (int)itemsSold,
            HourlySales = hourly
        };
    }

    public async Task<SalesReportVM> GetSalesReportAsync(DateRangeFilter filter)
    {
        var query = _db.StockMains
            .AsNoTracking()
            .Include(s => s.TransactionType)
            .Include(s => s.Party)
            .Where(s => (SaleCodes.Contains(s.TransactionType!.Code) || SaleReturnCodes.Contains(s.TransactionType!.Code))
                        && s.TransactionDate >= filter.FromDate
                        && s.TransactionDate < filter.ToDate.AddDays(1)
                        && s.Status != "Void");

        if (filter.PartyId.HasValue)
            query = query.Where(s => s.Party_ID == filter.PartyId.Value);

        var data = await query.ToListAsync();

        var rows = data.Select(s =>
        {
            var isReturn = SaleReturnCodes.Contains(s.TransactionType!.Code);
            var multiplier = isReturn ? -1 : 1;

            return new SalesReportRow
            {
                StockMainId = s.StockMainID,
                TransactionNo = s.TransactionNo,
                TransactionDate = s.TransactionDate,
                CustomerName = s.Party?.Name ?? "Walk-in Customer",
                SubTotal = s.SubTotal * multiplier,
                Discount = s.DiscountAmount * multiplier,
                TotalAmount = s.TotalAmount * multiplier,
                PaidAmount = s.PaidAmount * multiplier,
                // A return carries no receivable of its own: SaleReturnService already subtracts it
                // from the referenced sale's BalanceAmount. Negating the return's own balance here
                // would deduct the same credit twice and understate what customers still owe.
                BalanceAmount = isReturn ? 0m : s.BalanceAmount,
                Status = isReturn ? "Return" : s.Status
            };
        })
        .OrderByDescending(s => s.TransactionDate)
        .ToList();

        var vm = new SalesReportVM
        {
            Filter = filter,
            Rows = rows,
            GrandTotal = rows.Sum(r => r.TotalAmount),
            GrandDiscount = rows.Sum(r => r.Discount),
            GrandPaid = rows.Sum(r => r.PaidAmount),
            GrandBalance = rows.Sum(r => r.BalanceAmount)
        };

        return vm;
    }

    public async Task<SalesByProductVM> GetSalesByProductAsync(DateRangeFilter filter)
    {
        var query = _db.StockDetails
            .AsNoTracking()
            .Where(d => (SaleCodes.Contains(d.StockMain!.TransactionType!.Code) || SaleReturnCodes.Contains(d.StockMain.TransactionType!.Code))
                        && d.StockMain.TransactionDate >= filter.FromDate
                        && d.StockMain.TransactionDate < filter.ToDate.AddDays(1)
                        && d.StockMain.Status != "Void");

        if (filter.CategoryId.HasValue)
            query = query.Where(d => d.Product!.Category_ID == filter.CategoryId.Value);

        var groupedRows = await query
            .GroupBy(d => new
            {
                d.Product_ID,
                ProductName = d.Product!.Name,
                CategoryName = d.Product.Category != null ? d.Product.Category.Name : "",
                TransactionCode = d.StockMain!.TransactionType!.Code
            })
            .Select(g => new
            {
                ProductId = g.Key.Product_ID,
                ProductName = g.Key.ProductName,
                CategoryName = g.Key.CategoryName,
                Code = g.Key.TransactionCode,
                Quantity = g.Sum(d => d.Quantity),
                // LineTotal is net of line discounts but gross of the header discount, which is
                // only ever applied at header level. Apportion it pro-rata by line value, or this
                // report credits each product with revenue the customer never paid.
                Revenue = g.Sum(d => d.StockMain!.SubTotal == 0
                    ? d.LineTotal
                    : d.LineTotal - (d.LineTotal * d.StockMain.DiscountAmount / d.StockMain.SubTotal)),
                Cost = g.Sum(d => d.LineCost)
            })
            .ToListAsync();

        var rows = groupedRows
            .GroupBy(r => new { r.ProductId, r.ProductName, r.CategoryName })
            .Select(g =>
            {
                var netQuantity = g.Sum(x => SaleReturnCodes.Contains(x.Code) ? -x.Quantity : x.Quantity);
                var netRevenue = g.Sum(x => SaleReturnCodes.Contains(x.Code) ? -x.Revenue : x.Revenue);
                var netCost = g.Sum(x => SaleReturnCodes.Contains(x.Code) ? -x.Cost : x.Cost);
                var profit = netRevenue - netCost;

                return new SalesByProductRow
                {
                    ProductId = g.Key.ProductId,
                    ProductName = g.Key.ProductName,
                    CategoryName = g.Key.CategoryName,
                    QuantitySold = netQuantity,
                    Revenue = netRevenue,
                    Cost = netCost,
                    GrossProfit = profit,
                    ProfitMarginPercent = netRevenue == 0 ? 0 : Math.Round(profit / netRevenue * 100, 2)
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
        var transactions = await _db.StockMains
            .AsNoTracking()
            .Include(s => s.TransactionType)
            .Include(s => s.Party)
            .Where(s => (SaleCodes.Contains(s.TransactionType!.Code) || SaleReturnCodes.Contains(s.TransactionType!.Code))
                        && s.TransactionDate >= filter.FromDate
                        && s.TransactionDate < filter.ToDate.AddDays(1)
                        && s.Status != "Void"
                        && s.Party_ID != null)
            .ToListAsync();

        var rows = transactions
            .GroupBy(s => new
            {
                PartyId = s.Party_ID!.Value,
                CustomerName = s.Party != null ? s.Party.Name : "Unknown"
            })
            .Select(g =>
            {
                var totalSales = g.Sum(s => SaleReturnCodes.Contains(s.TransactionType!.Code) ? -s.TotalAmount : s.TotalAmount);
                var totalPaid = g.Sum(s => SaleReturnCodes.Contains(s.TransactionType!.Code) ? -s.PaidAmount : s.PaidAmount);
                // A return carries no receivable of its own: SaleReturnService already subtracts it
                // from the referenced sale's BalanceAmount. Negating the return's own balance here
                // would deduct the same credit twice and understate what the customer still owes.
                var balanceDue = g.Sum(s => SaleReturnCodes.Contains(s.TransactionType!.Code) ? 0m : s.BalanceAmount);

                return new SalesByCustomerRow
                {
                    PartyId = g.Key.PartyId,
                    CustomerName = g.Key.CustomerName,
                    PurchaseCount = g.Count(s => SaleCodes.Contains(s.TransactionType!.Code)),
                    TotalPurchases = totalSales,
                    TotalPaid = totalPaid,
                    BalanceDue = balanceDue,
                    LastPurchaseDate = g.Where(s => SaleCodes.Contains(s.TransactionType!.Code)).Max(s => (DateTime?)s.TransactionDate)
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
    
    public async Task<CustomerBalanceSummaryVM> GetCustomerBalanceSummaryAsync(DateTime asOfDate)
    {
        // "As of" means the close of that day. Rows carry a time of day, so comparing against a
        // midnight asOfDate would exclude everything transacted during the day being reported on.
        var cutoff = asOfDate.Date.AddDays(1);

        // Deactivating a customer must not erase what they owe: the receivables aging carries
        // no IsActive filter, so dropping inactive debtors here would make the two reports
        // disagree by the whole outstanding balance. Inactive customers are kept and surfaced
        // below whenever their balance is non-zero (same rule as inactive products holding stock).
        var customers = await _db.Parties
            .AsNoTracking()
            .Where(p => p.PartyType == "Customer")
            .Select(p => new
            {
                p.PartyID,
                p.Name,
                p.OpeningBalance,
                p.CreditLimit,
                p.IsActive
            })
            .ToListAsync();

        var salesByCustomer = await _db.StockMains
            .AsNoTracking()
            .Where(s => SaleCodes.Contains(s.TransactionType!.Code)
                        && s.TransactionDate < cutoff
                        && s.Status != "Void"
                        && s.Party_ID != null)
            .GroupBy(s => s.Party_ID!.Value)
            .Select(g => new
            {
                PartyId = g.Key,
                TotalSales = g.Sum(s => s.TotalAmount)
            })
            .ToListAsync();

        // Returned goods are no longer owed for. Without this the summary contradicts the
        // receivables aging, which reads the sale's already-netted BalanceAmount.
        var returnsByCustomer = await _db.StockMains
            .AsNoTracking()
            .Where(s => SaleReturnCodes.Contains(s.TransactionType!.Code)
                        && s.TransactionDate < cutoff
                        && s.Status != "Void"
                        && s.Party_ID != null)
            .GroupBy(s => s.Party_ID!.Value)
            .Select(g => new
            {
                PartyId = g.Key,
                TotalReturns = g.Sum(s => s.TotalAmount)
            })
            .ToListAsync();

        // Voided receipts were never collected — counting them would understate what the
        // customer still owes (and wrongly clear them against their credit limit).
        var receiptsByCustomer = await _db.Payments
            .AsNoTracking()
            .Where(p => p.PaymentType == PaymentType.RECEIPT.ToString()
                        && p.PaymentDate < cutoff
                        && !p.IsVoided)
            .GroupBy(p => p.Party_ID)
            .Select(g => new
            {
                PartyId = g.Key,
                TotalReceipts = g.Sum(p => p.Amount)
            })
            .ToListAsync();

        // Cash handed back to the customer cancels credit we owed them, so it offsets receipts.
        var refundsByCustomer = await _db.Payments
            .AsNoTracking()
            .Where(p => p.PaymentType == PaymentType.REFUND.ToString()
                        && p.PaymentDate < cutoff
                        && !p.IsVoided)
            .GroupBy(p => p.Party_ID)
            .Select(g => new
            {
                PartyId = g.Key,
                TotalRefunds = g.Sum(p => p.Amount)
            })
            .ToListAsync();

        var salesLookup = salesByCustomer.ToDictionary(x => x.PartyId, x => x.TotalSales);
        var returnsLookup = returnsByCustomer.ToDictionary(x => x.PartyId, x => x.TotalReturns);
        var receiptsLookup = receiptsByCustomer.ToDictionary(x => x.PartyId, x => x.TotalReceipts);
        var refundsLookup = refundsByCustomer.ToDictionary(x => x.PartyId, x => x.TotalRefunds);

        var rows = customers.Select(c =>
        {
            var grossSales = salesLookup.TryGetValue(c.PartyID, out var salesTotal) ? salesTotal : 0;
            var returns = returnsLookup.TryGetValue(c.PartyID, out var returnTotal) ? returnTotal : 0;
            var totalSales = grossSales - returns;

            // Single source of truth for cash collections: normalized Payment events.
            var receipts = receiptsLookup.TryGetValue(c.PartyID, out var receiptTotal) ? receiptTotal : 0;
            var refunds = refundsLookup.TryGetValue(c.PartyID, out var refundTotal) ? refundTotal : 0;
            var totalReceipts = receipts - refunds;

            var balance = totalSales - totalReceipts + c.OpeningBalance;

            return new
            {
                c.IsActive,
                Row = new CustomerBalanceRow
                {
                    PartyId = c.PartyID,
                    CustomerName = c.Name,
                    TotalSales = totalSales,
                    TotalReceipts = totalReceipts,
                    BalanceDue = balance,
                    CreditLimit = c.CreditLimit,
                    IsOverLimit = c.CreditLimit > 0 && balance > c.CreditLimit
                }
            };
        })
        .Where(x => x.Row.BalanceDue != 0 || (x.IsActive && x.Row.TotalSales > 0))
        .Select(x => x.Row)
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
