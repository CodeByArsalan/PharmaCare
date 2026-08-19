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
            .Where(s => (PurchaseCodes.Contains(s.TransactionType!.Code) || PurchaseReturnCodes.Contains(s.TransactionType!.Code))
                        && s.TransactionDate >= filter.FromDate
                        && s.TransactionDate < filter.ToDate.AddDays(1)
                        && s.Status != "Void");

        if (filter.PartyId.HasValue)
            query = query.Where(s => s.Party_ID == filter.PartyId.Value);

        // Project to the report row in SQL (negate return rows via CASE), selecting only the
        // columns we need instead of materializing full entity graphs.
        var rows = await query
            .OrderByDescending(s => s.TransactionDate)
            .Select(s => new PurchaseReportRow
            {
                StockMainId = s.StockMainID,
                TransactionNo = s.TransactionNo,
                TransactionDate = s.TransactionDate,
                SupplierName = s.Party != null ? s.Party.Name : "",
                SubTotal = PurchaseReturnCodes.Contains(s.TransactionType!.Code) ? -s.SubTotal : s.SubTotal,
                Discount = PurchaseReturnCodes.Contains(s.TransactionType!.Code) ? -s.DiscountAmount : s.DiscountAmount,
                TotalAmount = PurchaseReturnCodes.Contains(s.TransactionType!.Code) ? -s.TotalAmount : s.TotalAmount,
                PaidAmount = PurchaseReturnCodes.Contains(s.TransactionType!.Code) ? -s.PaidAmount : s.PaidAmount,
                // A return carries no payable of its own: PurchaseReturnService already subtracts it
                // from the referenced GRN's BalanceAmount. Negating the return's own balance here
                // would deduct the same credit twice and understate what is still owed to suppliers.
                BalanceAmount = PurchaseReturnCodes.Contains(s.TransactionType!.Code) ? 0m : s.BalanceAmount,
                Status = PurchaseReturnCodes.Contains(s.TransactionType!.Code) ? "Return" : s.Status
            })
            .ToListAsync();

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
        // Aggregate per supplier in SQL (GROUP BY) so only the per-supplier summary rows are
        // returned, instead of pulling every transaction in the range into memory.
        var rows = await _db.StockMains
            .Where(s => (PurchaseCodes.Contains(s.TransactionType!.Code) || PurchaseReturnCodes.Contains(s.TransactionType!.Code))
                        && s.TransactionDate >= filter.FromDate
                        && s.TransactionDate < filter.ToDate.AddDays(1)
                        && s.Status != "Void"
                        && s.Party_ID != null)
            .GroupBy(s => new { s.Party_ID, SupplierName = s.Party!.Name })
            .Select(g => new PurchaseBySupplierRow
            {
                PartyId = g.Key.Party_ID ?? 0,
                SupplierName = g.Key.SupplierName ?? "Unknown",
                // SUM(CASE ...) — count only purchases, negate returns for money columns.
                PurchaseCount = g.Sum(s => PurchaseCodes.Contains(s.TransactionType!.Code) ? 1 : 0),
                TotalPurchases = g.Sum(s => PurchaseReturnCodes.Contains(s.TransactionType!.Code) ? -s.TotalAmount : s.TotalAmount),
                TotalPaid = g.Sum(s => PurchaseReturnCodes.Contains(s.TransactionType!.Code) ? -s.PaidAmount : s.PaidAmount),
                BalanceDue = g.Sum(s => PurchaseReturnCodes.Contains(s.TransactionType!.Code) ? -s.BalanceAmount : s.BalanceAmount),
                // MAX over purchase rows only (returns map to NULL and are ignored by MAX).
                LastPurchaseDate = g.Max(s => PurchaseCodes.Contains(s.TransactionType!.Code) ? (DateTime?)s.TransactionDate : null)
            })
            .OrderByDescending(r => r.TotalPurchases)
            .ToListAsync();

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
