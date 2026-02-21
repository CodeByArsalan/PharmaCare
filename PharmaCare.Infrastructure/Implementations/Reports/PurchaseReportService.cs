using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces.Reports;
using PharmaCare.Application.ViewModels.Report;
using PharmaCare.Infrastructure;

namespace PharmaCare.Infrastructure.Implementations.Reports;

public class PurchaseReportService : IPurchaseReportService
{
    private readonly PharmaCareDBContext _db;
    private static readonly string[] PurchaseCodes = { "GRN" };
    // private static readonly string[] PurchaseReturnCodes = { "PRTN" }; // Unused in extracted methods so far

    public PurchaseReportService(PharmaCareDBContext db)
    {
        _db = db;
    }

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
}
