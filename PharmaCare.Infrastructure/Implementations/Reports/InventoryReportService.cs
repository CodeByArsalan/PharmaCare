using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces.Reports;
using PharmaCare.Application.ViewModels.Report;
using PharmaCare.Infrastructure;

namespace PharmaCare.Infrastructure.Implementations.Reports;

public class InventoryReportService : IInventoryReportService
{
    private readonly PharmaCareDBContext _db;
    
    // Transaction-type codes used for stock calculation
    private static readonly string[] SaleCodes = { "SALE" };
    private static readonly string[] SaleReturnCodes = { "SRTN" };
    private static readonly string[] PurchaseCodes = { "GRN" }; // Not used directly in logic below but good for consistency
    private static readonly string[] PurchaseReturnCodes = { "PRTN" };

    public InventoryReportService(PharmaCareDBContext db)
    {
        _db = db;
    }

    public async Task<CurrentStockReportVM> GetCurrentStockReportAsync(DateRangeFilter filter)
    {
        var productsQuery = _db.Products
            .AsNoTracking()
            .Where(p => p.IsActive);

        if (filter.CategoryId.HasValue)
            productsQuery = productsQuery.Where(p => p.Category_ID == filter.CategoryId.Value);

        var products = await productsQuery
            .Select(p => new
            {
                p.ProductID,
                p.Name,
                CategoryName = p.Category != null ? p.Category.Name : "",
                OpeningQty = (decimal)p.OpeningQuantity,
                p.OpeningPrice,
                p.ReorderLevel
            })
            .ToListAsync();

        var productIds = products.Select(p => p.ProductID).ToList();

        var movementRows = await _db.StockDetails
            .AsNoTracking()
            .Where(d => productIds.Contains(d.Product_ID) && d.StockMain!.Status != "Void")
            .GroupBy(d => d.Product_ID)
            .Select(g => new
            {
                ProductId = g.Key,
                PurchasedQty = g.Where(d => d.StockMain!.TransactionType!.StockDirection == 1)
                    .Sum(d => (decimal?)d.Quantity) ?? 0,
                SoldQty = g.Where(d => d.StockMain!.TransactionType!.StockDirection == -1
                                       && SaleCodes.Contains(d.StockMain.TransactionType!.Code))
                    .Sum(d => (decimal?)d.Quantity) ?? 0,
                ReturnedInQty = g.Where(d => d.StockMain!.TransactionType!.StockDirection == 1
                                             && SaleReturnCodes.Contains(d.StockMain.TransactionType!.Code))
                    .Sum(d => (decimal?)d.Quantity) ?? 0,
                ReturnedOutQty = g.Where(d => d.StockMain!.TransactionType!.StockDirection == -1
                                              && PurchaseReturnCodes.Contains(d.StockMain.TransactionType!.Code))
                    .Sum(d => (decimal?)d.Quantity) ?? 0
            })
            .ToListAsync();

        var movementByProduct = movementRows.ToDictionary(m => m.ProductId);

        var rows = new List<CurrentStockRow>(products.Count);
        foreach (var p in products)
        {
            movementByProduct.TryGetValue(p.ProductID, out var m);

            var purchasedQty = m?.PurchasedQty ?? 0;
            var soldQty = m?.SoldQty ?? 0;
            var returnedInQty = m?.ReturnedInQty ?? 0;
            var returnedOutQty = m?.ReturnedOutQty ?? 0;

            var currentStock = p.OpeningQty + purchasedQty - soldQty + returnedInQty - returnedOutQty;

            rows.Add(new CurrentStockRow
            {
                ProductId = p.ProductID,
                ProductName = p.Name,
                CategoryName = p.CategoryName,
                OpeningQty = p.OpeningQty,
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
}
