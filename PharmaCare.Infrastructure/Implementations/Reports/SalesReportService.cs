using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces.Reports;
using PharmaCare.Application.ViewModels.Report;
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
