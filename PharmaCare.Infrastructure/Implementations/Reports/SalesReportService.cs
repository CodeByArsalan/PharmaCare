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
                TotalPaid = g.Sum(s => s.PaidAmount),
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

        var cashCollected = dailyTotalsByType
            .Where(x => SaleCodes.Contains(x.Code))
            .Sum(x => x.TotalPaid);

        var outstanding = dailyTotalsByType
            .Where(x => SaleCodes.Contains(x.Code))
            .Sum(x => x.TotalBalance);

        var transactionCount = dailyTotalsByType
            .Where(x => SaleCodes.Contains(x.Code))
            .Sum(x => x.Count);

        var totalCOGS = await _db.StockDetails
            .AsNoTracking()
            .Where(d => d.StockMain!.TransactionDate >= dayStart
                        && d.StockMain.TransactionDate < dayEnd
                        && d.StockMain.Status != "Void"
                        && SaleCodes.Contains(d.StockMain.TransactionType!.Code))
            .SumAsync(d => (decimal?)d.LineCost) ?? 0;

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
            .AsNoTracking()
            .Where(d => SaleCodes.Contains(d.StockMain!.TransactionType!.Code)
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
                CategoryName = d.Product.Category != null ? d.Product.Category.Name : ""
            })
            .Select(g => new
            {
                ProductId = g.Key.Product_ID,
                ProductName = g.Key.ProductName,
                CategoryName = g.Key.CategoryName,
                QuantitySold = g.Sum(d => d.Quantity),
                Revenue = g.Sum(d => d.LineTotal),
                Cost = g.Sum(d => d.LineCost)
            })
            .OrderByDescending(r => r.Revenue)
            .ToListAsync();

        var rows = groupedRows
            .Select(r =>
            {
                var profit = r.Revenue - r.Cost;
                return new SalesByProductRow
                {
                    ProductId = r.ProductId,
                    ProductName = r.ProductName,
                    CategoryName = r.CategoryName,
                    QuantitySold = r.QuantitySold,
                    Revenue = r.Revenue,
                    Cost = r.Cost,
                    GrossProfit = profit,
                    ProfitMarginPercent = r.Revenue == 0 ? 0 : Math.Round(profit / r.Revenue * 100, 2)
                };
            })
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
        var rows = await _db.StockMains
            .AsNoTracking()
            .Where(s => SaleCodes.Contains(s.TransactionType!.Code)
                        && s.TransactionDate >= filter.FromDate
                        && s.TransactionDate < filter.ToDate.AddDays(1)
                        && s.Status != "Void"
                        && s.Party_ID != null)
            .GroupBy(s => new
            {
                PartyId = s.Party_ID!.Value,
                CustomerName = s.Party != null ? s.Party.Name : "Unknown"
            })
            .Select(g => new SalesByCustomerRow
            {
                PartyId = g.Key.PartyId,
                CustomerName = g.Key.CustomerName,
                PurchaseCount = g.Count(),
                TotalPurchases = g.Sum(s => s.TotalAmount),
                TotalPaid = g.Sum(s => s.PaidAmount),
                BalanceDue = g.Sum(s => s.BalanceAmount),
                LastPurchaseDate = g.Max(s => s.TransactionDate)
            })
            .OrderByDescending(r => r.TotalPurchases)
            .ToListAsync();

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
