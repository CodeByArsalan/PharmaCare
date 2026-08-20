using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Application.Interfaces.Reports;
using PharmaCare.Application.ViewModels.Report;
using PharmaCare.Infrastructure;

namespace PharmaCare.Infrastructure.Implementations.Reports;

public class InventoryReportService : IInventoryReportService
{
    private readonly PharmaCareDBContext _db;
    private readonly IProductService _productService;

    // Transaction-type codes used ONLY to break the movement down into display columns.
    // They must never decide the stock total itself — that is derived from StockDirection so a
    // newly seeded transaction type cannot silently go missing (which is what happened to the
    // SADJ+/SADJ- adjustments below).
    private static readonly string[] SaleCodes = { "SALE" };
    private static readonly string[] SaleReturnCodes = { "SRTN" };
    private static readonly string[] PurchaseCodes = { "GRN" };
    private static readonly string[] PurchaseReturnCodes = { "PRTN" };
    private static readonly string[] AdjustmentCodes = { "SADJ+", "SADJ-" };

    public InventoryReportService(PharmaCareDBContext db, IProductService productService)
    {
        _db = db;
        _productService = productService;
    }

    public async Task<CurrentStockReportVM> GetCurrentStockReportAsync(DateRangeFilter filter)
    {
        // Deactivating a product is a CATALOGUE gesture — it stops the till offering it. It does
        // not move any stock, and the general ledger still carries the value of whatever is left on
        // the shelf. Filtering those rows out made that value vanish from the valuation while the
        // stock control account was unchanged, so this report and the balance sheet disagreed by
        // the whole amount. Inactive products are therefore included whenever they still hold
        // stock, and only dropped once they are genuinely empty.
        var productsWithMovement = _db.StockDetails
            .AsNoTracking()
            .Where(d => d.StockMain!.Status == "Approved"
                        && d.StockMain.TransactionType!.AffectsStock
                        && d.StockMain.TransactionType.IsActive)
            .Select(d => d.Product_ID);

        var productsQuery = _db.Products
            .AsNoTracking()
            .Where(p => p.IsActive
                        || p.OpeningQuantity != 0
                        || productsWithMovement.Contains(p.ProductID));

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

        // Status == "Approved" (not != "Void") and AffectsStock/StockDirection are the SAME rules
        // ProductService applies for POS availability. Anything else makes this report disagree
        // with the quantity the till will actually sell.
        // Grouped by product + transaction type, then aggregated in memory — the same shape
        // ProductService.GetProductsWithStockAsync uses. Keeping the SQL to one simple GROUP BY
        // avoids leaning on EF to translate several conditional aggregates over one group.
        var movementRows = await _db.StockDetails
            .AsNoTracking()
            .Where(d => productIds.Contains(d.Product_ID)
                        && d.StockMain!.Status == "Approved"
                        && d.StockMain.TransactionType!.AffectsStock
                        && d.StockMain.TransactionType.IsActive)
            .GroupBy(d => new
            {
                d.Product_ID,
                Code = d.StockMain!.TransactionType!.Code,
                d.StockMain.TransactionType.StockDirection
            })
            .Select(g => new
            {
                g.Key.Product_ID,
                g.Key.Code,
                g.Key.StockDirection,
                Quantity = g.Sum(d => d.Quantity)
            })
            .ToListAsync();

        var movementByProduct = movementRows
            .GroupBy(m => m.Product_ID)
            .ToDictionary(g => g.Key, g => new
            {
                // Authoritative total: every stock-affecting type, signed by its own direction.
                NetQty = g.Sum(m => m.Quantity * m.StockDirection),

                // Presentation-only breakdown.
                PurchasedQty = g.Where(m => PurchaseCodes.Contains(m.Code)).Sum(m => m.Quantity),
                SoldQty = g.Where(m => SaleCodes.Contains(m.Code)).Sum(m => m.Quantity),
                ReturnedInQty = g.Where(m => SaleReturnCodes.Contains(m.Code)).Sum(m => m.Quantity),
                ReturnedOutQty = g.Where(m => PurchaseReturnCodes.Contains(m.Code)).Sum(m => Math.Abs(m.Quantity)),
                AdjustedQty = g.Where(m => AdjustmentCodes.Contains(m.Code)).Sum(m => m.Quantity * m.StockDirection)
            });

        // Value stock at what it actually cost (latest approved GRN, falling back to opening
        // price) rather than the opening price alone, which ignored every purchase since.
        // Reuses the single cost resolver rather than adding another implementation of it,
        // scoped to the products this report actually covers (respects the category filter).
        var costByProduct = await _productService.GetLastGrnCostPricesAsync(productIds);

        var rows = new List<CurrentStockRow>(products.Count);
        foreach (var p in products)
        {
            movementByProduct.TryGetValue(p.ProductID, out var m);

            var purchasedQty = m?.PurchasedQty ?? 0;
            var soldQty = m?.SoldQty ?? 0;
            var returnedInQty = m?.ReturnedInQty ?? 0;
            var returnedOutQty = m?.ReturnedOutQty ?? 0;
            var adjustedQty = m?.AdjustedQty ?? 0;

            var currentStock = p.OpeningQty + (m?.NetQty ?? 0);
            var costPrice = costByProduct.TryGetValue(p.ProductID, out var c) ? c : p.OpeningPrice;

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
                AdjustedQty = adjustedQty,
                CurrentStock = currentStock,
                CostPrice = costPrice,
                StockValue = Math.Round(currentStock * costPrice, 2),
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

        // Same movement rule as GetCurrentStockReportAsync and ProductService, so this report's
        // closing balance reconciles with current stock and POS availability.
        var details = await _db.StockDetails
            .Include(d => d.StockMain).ThenInclude(s => s!.TransactionType)
            .Where(d => d.Product_ID == filter.ProductId.Value
                        && d.StockMain!.Status == "Approved"
                        && d.StockMain.TransactionType!.AffectsStock
                        && d.StockMain.TransactionType.IsActive
                        && d.StockMain.TransactionDate >= filter.FromDate
                        && d.StockMain.TransactionDate < filter.ToDate.AddDays(1))
            .OrderBy(d => d.StockMain!.TransactionDate)
            .ThenBy(d => d.StockDetailID)
            .ToListAsync();

        // Calculate opening balance (movements before FromDate + opening qty)
        var priorDetails = await _db.StockDetails
            .Include(d => d.StockMain).ThenInclude(s => s!.TransactionType)
            .Where(d => d.Product_ID == filter.ProductId.Value
                        && d.StockMain!.Status == "Approved"
                        && d.StockMain.TransactionType!.AffectsStock
                        && d.StockMain.TransactionType.IsActive
                        && d.StockMain.TransactionDate < filter.FromDate)
            .ToListAsync();

        decimal openingBalance = product.OpeningQuantity;
        foreach (var d in priorDetails)
        {
            var dir = d.StockMain!.TransactionType!.StockDirection;
            openingBalance += Math.Abs(d.Quantity) * dir;
        }

        var runningBalance = openingBalance;
        var rows = new List<ProductMovementRow>();
        foreach (var d in details)
        {
            var dir = d.StockMain!.TransactionType!.StockDirection;
            var qtyIn = dir > 0 ? Math.Abs(d.Quantity) : 0;
            var qtyOut = dir < 0 ? Math.Abs(d.Quantity) : 0;
            runningBalance += Math.Abs(d.Quantity) * dir;

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
        var cutoffDate = AppTime.Today.AddDays(-thresholdDays);

        // Get current stock first
        var stockReport = await GetCurrentStockReportAsync(filter);

        // Get last sale dates per product
        var lastSaleDates = await _db.StockDetails
            .Include(d => d.StockMain).ThenInclude(s => s!.TransactionType)
            .Where(d => SaleCodes.Contains(d.StockMain!.TransactionType!.Code)
                        && d.StockMain.Status == "Approved")
            .GroupBy(d => d.Product_ID)
            .Select(g => new { ProductId = g.Key, LastDate = g.Max(d => d.StockMain!.TransactionDate) })
            .ToDictionaryAsync(x => x.ProductId, x => x.LastDate);

        var rows = stockReport.Rows
            .Where(r => r.CurrentStock > 0)
            .Select(r =>
            {
                var found = lastSaleDates.TryGetValue(r.ProductId, out var lastDate);
                var daysSince = found ? (int)(AppTime.Today - lastDate).TotalDays : 9999;
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
