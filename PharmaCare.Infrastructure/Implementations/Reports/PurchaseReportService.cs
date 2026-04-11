using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces.Reports;
using PharmaCare.Application.ViewModels.Report;
using PharmaCare.Infrastructure;

namespace PharmaCare.Infrastructure.Implementations.Reports;

public class PurchaseReportService : IPurchaseReportService
{
    private readonly PharmaCareDBContext _db;
    private static readonly string[] PurchaseCodes = { "GRN" };
    private static readonly string[] PurchaseReturnCodes = { "PRTN" };

    public PurchaseReportService(PharmaCareDBContext db)
    {
        _db = db;
    }

    public async Task<PurchaseReportVM> GetPurchaseReportAsync(DateRangeFilter filter)
    {
        var query = _db.StockMains
            .Include(s => s.TransactionType)
            .Include(s => s.Party)
            .Where(s => (PurchaseCodes.Contains(s.TransactionType!.Code) || PurchaseReturnCodes.Contains(s.TransactionType!.Code))
                        && s.TransactionDate >= filter.FromDate
                        && s.TransactionDate < filter.ToDate.AddDays(1)
                        && s.Status != "Void");

        if (filter.PartyId.HasValue)
            query = query.Where(s => s.Party_ID == filter.PartyId.Value);

        var data = await query.ToListAsync();

        var rows = data.Select(s =>
        {
            var isReturn = PurchaseReturnCodes.Contains(s.TransactionType!.Code);
            var multiplier = isReturn ? -1 : 1;

            return new PurchaseReportRow
            {
                StockMainId = s.StockMainID,
                TransactionNo = s.TransactionNo,
                TransactionDate = s.TransactionDate,
                SupplierName = s.Party?.Name ?? "",
                SubTotal = s.SubTotal * multiplier,
                Discount = s.DiscountAmount * multiplier,
                TotalAmount = s.TotalAmount * multiplier,
                PaidAmount = s.PaidAmount * multiplier,
                BalanceAmount = s.BalanceAmount * multiplier,
                Status = isReturn ? "Return" : s.Status
            };
        })
        .OrderByDescending(s => s.TransactionDate)
        .ToList();

        var vm = new PurchaseReportVM
        {
            Filter = filter,
            Rows = rows,
            GrandTotal = rows.Sum(r => r.TotalAmount),
            GrandDiscount = rows.Sum(r => r.Discount),
            GrandPaid = rows.Sum(r => r.PaidAmount),
            GrandBalance = rows.Sum(r => r.BalanceAmount)
        };

        // Re-check VM field names for PurchaseReportVM
        return vm;
    }

    public async Task<PurchaseBySupplierVM> GetPurchaseBySupplierAsync(DateRangeFilter filter)
    {
        var purchases = await _db.StockMains
            .Include(s => s.TransactionType)
            .Include(s => s.Party)
            .Where(s => (PurchaseCodes.Contains(s.TransactionType!.Code) || PurchaseReturnCodes.Contains(s.TransactionType!.Code))
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
                var totalPurchases = g.Sum(s => PurchaseReturnCodes.Contains(s.TransactionType!.Code) ? -s.TotalAmount : s.TotalAmount);
                var totalPaid = g.Sum(s => PurchaseReturnCodes.Contains(s.TransactionType!.Code) ? -s.PaidAmount : s.PaidAmount);
                var balanceDue = g.Sum(s => PurchaseReturnCodes.Contains(s.TransactionType!.Code) ? -s.BalanceAmount : s.BalanceAmount);

                return new PurchaseBySupplierRow
                {
                    PartyId = first.Party_ID ?? 0,
                    SupplierName = first.Party?.Name ?? "Unknown",
                    PurchaseCount = g.Count(s => PurchaseCodes.Contains(s.TransactionType!.Code)),
                    TotalPurchases = totalPurchases,
                    TotalPaid = totalPaid,
                    BalanceDue = balanceDue,
                    LastPurchaseDate = g.Where(s => PurchaseCodes.Contains(s.TransactionType!.Code)).Max(s => (DateTime?)s.TransactionDate)
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
