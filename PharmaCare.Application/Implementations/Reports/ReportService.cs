using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.DTOs.Reports;
using PharmaCare.Application.Interfaces.Reports;
using PharmaCare.Domain.Models.SaleManagement;
using PharmaCare.Domain.Models.Inventory;
using PharmaCare.Domain.Models.PurchaseManagement;
using PharmaCare.Domain.Models.Products;
using PharmaCare.Infrastructure.Interfaces;

namespace PharmaCare.Application.Implementations.Reports;

public class ReportService(IRepository<Sale> _saleRepo, IRepository<SaleLine> _saleLineRepo, IRepository<StoreInventory> _inventoryRepo,
        IRepository<Product> _productRepo, IRepository<StockMovement> _movementRepo) : IReportService
{

    public async Task<SalesReportDto> GetSalesReport(DateTime startDate, DateTime endDate, int? storeId = null)
    {
        var sales = await _saleRepo.FindByCondition(s => s.SaleDate >= startDate && s.SaleDate <= endDate && (!storeId.HasValue || s.Store_ID == storeId.Value))
            .Include(s => s.SaleLines)
            .Include(s => s.Payments)
            .ToListAsync();

        var dailySales = sales
            .GroupBy(s => s.SaleDate.Date)
            .Select(g => new DailySalesDto
            {
                Date = g.Key,
                TotalAmount = g.Sum(s => s.Total),
                TransactionCount = g.Count()
            })
            .OrderBy(d => d.Date)
            .ToList();

        var paymentBreakdown = sales
            .SelectMany(s => s.Payments)
            .GroupBy(p => p.PaymentMethod)
            .Select(g => new PaymentMethodBreakdownDto
            {
                PaymentMethod = g.Key,
                TotalAmount = g.Sum(p => p.Amount),
                TransactionCount = g.Count()
            })
            .ToList();

        var topProducts = await _saleLineRepo.FindByCondition(sl => sl.Sale != null && sl.Sale!.SaleDate >= startDate && sl.Sale!.SaleDate <= endDate)
            .Include(sl => sl.Product)
            .GroupBy(sl => new { sl.Product_ID, ProductName = sl.Product != null ? sl.Product.ProductName : "Unknown" })
            .Select(g => new TopProductDto
            {
                ProductID = g.Key.Product_ID ?? 0,
                ProductName = g.Key.ProductName,
                QuantitySold = g.Sum(sl => sl.Quantity),
                TotalRevenue = g.Sum(sl => sl.Quantity * sl.UnitPrice)
            })
            .OrderByDescending(p => p.TotalRevenue)
            .Take(10)
            .ToListAsync();

        return new SalesReportDto
        {
            StartDate = startDate,
            EndDate = endDate,
            TotalSales = sales.Sum(s => s.Total),
            TotalTransactions = sales.Count,
            AverageTransactionValue = sales.Any() ? sales.Average(s => s.Total) : 0,
            DailySales = dailySales,
            PaymentMethodBreakdown = paymentBreakdown,
            TopProducts = topProducts
        };
    }

    public async Task<InventoryReportDto> GetInventoryReport(int? storeId = null)
    {
        var stockLevels = await _inventoryRepo.FindByCondition(si => si.QuantityOnHand > 0 && (!storeId.HasValue || si.Store_ID == storeId.Value))
            .Include(si => si.Store)
            .Include(si => si.ProductBatch)
                .ThenInclude(pb => pb.Product)
            .Select(si => new StockLevelDto
            {
                ProductID = si.ProductBatch.Product_ID ?? 0,
                ProductName = si.ProductBatch.Product != null ? si.ProductBatch.Product.ProductName : "Unknown",
                StoreName = si.Store != null ? si.Store.Name : "Unknown",
                BatchNumber = si.ProductBatch.BatchNumber,
                QuantityOnHand = si.QuantityOnHand,
                ExpiryDate = si.ProductBatch.ExpiryDate,
                UnitPrice = si.ProductBatch.CostPrice,
                TotalValue = si.QuantityOnHand * si.ProductBatch.CostPrice
            })
            .ToListAsync();

        var lowStockItems = await _productRepo.FindByCondition(p => p.IsActive)
            .Select(p => new
            {
                p.ProductID,
                p.ProductName,
                CurrentStock = p.ProductBatches.SelectMany(pb => pb.StoreInventories)
                    .Sum(si => (decimal?)si.QuantityOnHand) ?? 0,
                ReorderLevel = p.ReorderLevel ?? 10
            })
            .Where(p => p.CurrentStock < p.ReorderLevel)
            .Select(p => new LowStockItemDto
            {
                ProductID = p.ProductID,
                ProductName = p.ProductName,
                CurrentStock = p.CurrentStock,
                ReorderLevel = p.ReorderLevel
            })
            .ToListAsync();

        var expiringItemsRaw = await _inventoryRepo.FindByCondition(si => si.QuantityOnHand > 0 &&
                    si.ProductBatch.ExpiryDate <= DateTime.Now.AddDays(90))
            .Include(si => si.ProductBatch)
                .ThenInclude(pb => pb.Product)
            .ToListAsync();

        var expiringItems = expiringItemsRaw
            .Select(si => new ExpiringItemDto
            {
                ProductBatchID = si.ProductBatch_ID,
                ProductName = si.ProductBatch.Product != null ? si.ProductBatch.Product.ProductName : "Unknown",
                BatchNumber = si.ProductBatch.BatchNumber,
                ExpiryDate = si.ProductBatch.ExpiryDate,
                QuantityOnHand = si.QuantityOnHand,
                DaysUntilExpiry = (int)(si.ProductBatch.ExpiryDate - DateTime.Now).TotalDays
            })
            .OrderBy(e => e.DaysUntilExpiry)
            .ToList();

        return new InventoryReportDto
        {
            TotalProducts = await _productRepo.FindByCondition(p => p.IsActive).CountAsync(),
            LowStockCount = lowStockItems.Count,
            OutOfStockCount = await _productRepo.FindByCondition(p => p.IsActive &&
                !p.ProductBatches.Any(pb => pb.StoreInventories.Any(si => si.QuantityOnHand > 0))).CountAsync(),
            TotalStockValue = stockLevels.Sum(sl => sl.TotalValue),
            StockLevels = stockLevels,
            LowStockItems = lowStockItems,
            ExpiringItems = expiringItems
        };
    }

    public async Task<List<SalesDetailDto>> GetSalesDetailReport(DateTime startDate, DateTime endDate, int? storeId = null)
    {
        return await _saleLineRepo.FindByCondition(sl => sl.Sale != null && sl.Sale.SaleDate >= startDate && sl.Sale.SaleDate <= endDate && (!storeId.HasValue || sl.Sale.Store_ID == storeId.Value))
            .Include(sl => sl.Product)
                .ThenInclude(p => p.SubCategory)
            .Include(sl => sl.Sale)
            .GroupBy(sl => new
            {
                sl.Product_ID,
                ProductName = sl.Product.ProductName,
                SubCategory = sl.Product.SubCategory!.SubCategoryName ?? "Uncategorized"
            })
            .Select(g => new SalesDetailDto
            {
                ProductID = g.Key.Product_ID ?? 0,
                ProductName = g.Key.ProductName,
                Category = g.Key.SubCategory,
                QuantitySold = g.Sum(sl => sl.Quantity),
                TotalRevenue = g.Sum(sl => sl.Quantity * sl.UnitPrice),
                AveragePrice = g.Average(sl => sl.UnitPrice),
                TransactionCount = g.Select(sl => sl.Sale_ID).Distinct().Count()
            })
            .OrderByDescending(sd => sd.TotalRevenue)
            .ToListAsync();
    }

    public async Task<List<StockMovementDto>> GetStockMovementReport(DateTime startDate, DateTime endDate, string? productName = null, int? storeId = null)
    {
        var baseQuery = _movementRepo.FindByCondition(sm => sm.CreatedDate >= startDate && sm.CreatedDate <= endDate && (!storeId.HasValue || sm.Store_ID == storeId.Value));

        if (!string.IsNullOrEmpty(productName))
        {
            baseQuery = baseQuery.Where(sm => sm.ProductBatch.Product.ProductName.Contains(productName));
        }

        return await baseQuery
            .Include(sm => sm.ProductBatch)
                .ThenInclude(pb => pb.Product)
            .OrderBy(sm => sm.CreatedDate)
            .Select(sm => new StockMovementDto
            {
                MovementDate = sm.CreatedDate,
                ProductName = sm.ProductBatch.Product.ProductName,
                BatchNumber = sm.ProductBatch.BatchNumber,
                MovementType = sm.MovementType,
                Quantity = sm.Quantity,
                BalanceAfter = 0, // Running balance would need additional calculation
                Reference = sm.MovementType == "Sale" ? "Sale" : sm.MovementType == "Purchase" ? "GRN" : "Adjustment"
            })
            .ToListAsync();
    }

    // ===== NEW REPORT IMPLEMENTATIONS =====

    public async Task<List<SlowMovingItemDto>> GetSlowMovingItemsReport(int daysThreshold = 30, int? storeId = null)
    {
        var cutoffDate = DateTime.Now.AddDays(-daysThreshold);
        var cutoff90Days = DateTime.Now.AddDays(-90);

        var products = await _productRepo.FindByCondition(p => p.IsActive)
            .Include(p => p.SubCategory)
            .Include(p => p.ProductBatches)
                .ThenInclude(pb => pb.StoreInventories)
            .ToListAsync();

        var salesLast30Days = await _saleLineRepo.FindByCondition(sl => sl.Sale.SaleDate >= cutoffDate && (!storeId.HasValue || sl.Sale.Store_ID == storeId.Value))
            .ToListAsync();

        var salesLast90Days = await _saleLineRepo.FindByCondition(sl => sl.Sale.SaleDate >= cutoff90Days && (!storeId.HasValue || sl.Sale.Store_ID == storeId.Value))
            .ToListAsync();

        var result = products.Select(p =>
        {
            var currentStock = p.ProductBatches.SelectMany(pb => pb.StoreInventories)
                .Where(si => !storeId.HasValue || si.Store_ID == storeId.Value)
                .Sum(si => si.QuantityOnHand);

            var sold30 = salesLast30Days.Where(sl => sl.Product_ID == p.ProductID).Sum(sl => sl.Quantity);
            var sold90 = salesLast90Days.Where(sl => sl.Product_ID == p.ProductID).Sum(sl => sl.Quantity);
            var avgDailySales = sold90 / 90m;
            var daysOfStock = avgDailySales > 0 ? currentStock / avgDailySales : 999;

            return new SlowMovingItemDto
            {
                ProductID = p.ProductID,
                ProductName = p.ProductName,
                Category = p.SubCategory?.SubCategoryName ?? "Uncategorized",
                CurrentStock = currentStock,
                QuantitySoldLast30Days = sold30,
                QuantitySoldLast90Days = sold90,
                DaysOfStock = daysOfStock,
                StockValue = currentStock * (p.ProductBatches.FirstOrDefault()?.SellingPrice ?? 0)
            };
        })
        .Where(x => x.CurrentStock > 0 && x.QuantitySoldLast30Days < 5) // Less than 5 sold = slow moving
        .OrderByDescending(x => x.DaysOfStock)
        .ToList();

        return result;
    }

    public async Task<PurchaseReportDto> GetPurchaseReport(DateTime startDate, DateTime endDate, int? storeId = null)
    {
        // Query StockMovements of type 'Purchase'
        var movements = await _movementRepo.FindByCondition(sm => sm.CreatedDate >= startDate && sm.CreatedDate <= endDate &&
                (sm.MovementType == "Purchase" || sm.MovementType == "GRN") &&
                (!storeId.HasValue || sm.Store_ID == storeId.Value))
            .Include(sm => sm.ProductBatch)
                .ThenInclude(pb => pb!.Grn)
                    .ThenInclude(g => g!.PurchaseOrder)
                        .ThenInclude(p => p!.Party)
            .ToListAsync();

        var bySupplier = movements
            .Where(m => m.ProductBatch?.Grn?.PurchaseOrder?.Party != null)
            .GroupBy(m => new { m.ProductBatch!.Grn!.PurchaseOrder!.Party!.PartyID, m.ProductBatch.Grn.PurchaseOrder.Party.PartyName })
            .Select(g => new SupplierPurchaseDto
            {
                SupplierID = g.Key.PartyID,
                SupplierName = g.Key.PartyName,
                TotalAmount = g.Sum(m => m.Quantity * (m.ProductBatch?.CostPrice ?? 0)),
                OrderCount = g.Select(m => m.ProductBatch!.Grn_ID).Distinct().Count()
            })
            .OrderByDescending(s => s.TotalAmount)
            .ToList();

        var dailyPurchases = movements
            .GroupBy(m => m.CreatedDate.Date)
            .Select(g => new DailyPurchaseDto
            {
                Date = g.Key,
                TotalAmount = g.Sum(m => m.Quantity * (m.ProductBatch?.CostPrice ?? 0)),
                OrderCount = g.Select(m => m.ProductBatch!.Grn_ID).Distinct().Count()
            })
            .OrderBy(d => d.Date)
            .ToList();

        return new PurchaseReportDto
        {
            StartDate = startDate,
            EndDate = endDate,
            TotalPurchases = movements.Sum(m => m.Quantity * (m.ProductBatch?.CostPrice ?? 0)),
            TotalOrders = movements.Select(m => m.ProductBatch!.Grn_ID).Distinct().Count(),
            TotalItemsReceived = movements.Sum(m => m.Quantity),
            BySupplier = bySupplier,
            DailyPurchases = dailyPurchases
        };
    }

    public async Task<ProfitLossDto> GetProfitLossReport(DateTime startDate, DateTime endDate, int? storeId = null)
    {
        // Get sales with Total (which includes discounts)
        var sales = await _saleRepo.FindByCondition(s => s.SaleDate >= startDate && s.SaleDate <= endDate && (!storeId.HasValue || s.Store_ID == storeId.Value))
            .ToListAsync();


        var salesData = await _saleLineRepo.FindByCondition(sl => sl.Sale != null && sl.Sale.SaleDate >= startDate && sl.Sale.SaleDate <= endDate && (!storeId.HasValue || sl.Sale.Store_ID == storeId.Value))
            .Include(sl => sl.Product)
                .ThenInclude(p => p!.SubCategory)
            .Include(sl => sl.ProductBatch)
            .Include(sl => sl.Sale)
            .ToListAsync();

        // Total Revenue from Sales.Total (reflects discounts)
        var totalRevenue = sales.Sum(s => s.Total);
        var totalCost = salesData.Sum(sl => sl.Quantity * (sl.ProductBatch?.CostPrice ?? sl.UnitPrice * 0.7m)); // Use batch CostPrice or estimate 30% margin
        var grossProfit = totalRevenue - totalCost;

        // Calculate proportional revenue for each sale line based on Sale.Total (reflects discounts)
        // First, create a lookup for each sale's discount ratio
        var saleDiscountRatios = sales.ToDictionary(
            s => s.SaleID,
            s => s.SubTotal > 0 ? s.Total / s.SubTotal : 1m);

        var byCategory = salesData
            .GroupBy(sl => sl.Product?.SubCategory?.SubCategoryName ?? "Uncategorized")
            .Select(g =>
            {
                // Calculate proportional revenue: line amount * (Sale.Total / Sale.SubTotal)
                var categoryRevenue = g.Sum(sl =>
                {
                    var lineAmount = sl.Quantity * sl.UnitPrice;
                    var discountRatio = sl.Sale_ID.HasValue && saleDiscountRatios.ContainsKey(sl.Sale_ID.Value)
                        ? saleDiscountRatios[sl.Sale_ID.Value]
                        : 1m;
                    return lineAmount * discountRatio;
                });
                var categoryCost = g.Sum(sl => sl.Quantity * (sl.ProductBatch?.CostPrice ?? sl.UnitPrice * 0.7m));
                var categoryProfit = categoryRevenue - categoryCost;

                return new CategoryProfitDto
                {
                    Category = g.Key,
                    Revenue = categoryRevenue,
                    Cost = categoryCost,
                    Profit = categoryProfit,
                    Margin = categoryRevenue > 0 ? (categoryProfit / categoryRevenue) * 100 : 0
                };
            })
            .OrderByDescending(c => c.Profit)
            .ToList();


        // Daily profit using Sale.Total for revenue
        var saleIdsToData = salesData.GroupBy(sl => sl.Sale_ID).ToDictionary(g => g.Key ?? 0, g => g.Sum(sl => sl.Quantity * (sl.ProductBatch?.CostPrice ?? sl.UnitPrice * 0.7m)));
        var dailyProfit = sales
            .GroupBy(s => s.SaleDate.Date)
            .Select(g => new DailyProfitDto
            {
                Date = g.Key,
                Revenue = g.Sum(s => s.Total),
                Cost = g.Sum(s => saleIdsToData.GetValueOrDefault(s.SaleID, 0)),
                Profit = g.Sum(s => s.Total) - g.Sum(s => saleIdsToData.GetValueOrDefault(s.SaleID, 0))
            })
            .OrderBy(d => d.Date)
            .ToList();

        return new ProfitLossDto
        {
            StartDate = startDate,
            EndDate = endDate,
            TotalRevenue = totalRevenue,
            CostOfGoodsSold = totalCost,
            GrossProfit = grossProfit,
            GrossProfitMargin = totalRevenue > 0 ? (grossProfit / totalRevenue) * 100 : 0,
            ByCategory = byCategory,
            DailyProfit = dailyProfit
        };
    }

    public async Task<CustomerAnalyticsDto> GetCustomerAnalyticsReport(DateTime startDate, DateTime endDate, int? storeId = null)
    {
        var sales = await _saleRepo.FindByCondition(s => s.SaleDate >= startDate && s.SaleDate <= endDate && s.Party_ID.HasValue && (!storeId.HasValue || s.Store_ID == storeId.Value))
            .Include(s => s.Party)
            .ToListAsync();

        var customerSales = sales
            .GroupBy(s => s.Party)
            .Where(g => g.Key != null)
            .Select(g => new TopCustomerDto
            {
                CustomerID = g.Key!.PartyID,
                CustomerName = g.Key.PartyName,
                Phone = g.Key.ContactNumber ?? "",
                TotalSpent = g.Sum(s => s.Total),
                OrderCount = g.Count()
            })
            .OrderByDescending(c => c.TotalSpent)
            .ToList();

        var totalRevenue = customerSales.Sum(c => c.TotalSpent);

        // Segment customers: High (top 20%), Medium (next 30%), Low (bottom 50%)
        var highThreshold = customerSales.Count > 0 ? customerSales.Take((int)(customerSales.Count * 0.2)).LastOrDefault()?.TotalSpent ?? 0 : 0;
        var mediumThreshold = customerSales.Count > 0 ? customerSales.Take((int)(customerSales.Count * 0.5)).LastOrDefault()?.TotalSpent ?? 0 : 0;

        var segments = new List<CustomerSegmentDto>
        {
            new() { Segment = "High Value", CustomerCount = customerSales.Count(c => c.TotalSpent >= highThreshold), TotalRevenue = customerSales.Where(c => c.TotalSpent >= highThreshold).Sum(c => c.TotalSpent) },
            new() { Segment = "Medium Value", CustomerCount = customerSales.Count(c => c.TotalSpent < highThreshold && c.TotalSpent >= mediumThreshold), TotalRevenue = customerSales.Where(c => c.TotalSpent < highThreshold && c.TotalSpent >= mediumThreshold).Sum(c => c.TotalSpent) },
            new() { Segment = "Low Value", CustomerCount = customerSales.Count(c => c.TotalSpent < mediumThreshold), TotalRevenue = customerSales.Where(c => c.TotalSpent < mediumThreshold).Sum(c => c.TotalSpent) }
        };

        foreach (var seg in segments)
        {
            seg.PercentageOfTotal = totalRevenue > 0 ? (seg.TotalRevenue / totalRevenue) * 100 : 0;
        }

        return new CustomerAnalyticsDto
        {
            StartDate = startDate,
            EndDate = endDate,
            TotalCustomers = customerSales.Count,
            NewCustomersThisPeriod = 0, // Would need RegisteredDate check
            ActiveCustomers = customerSales.Count(c => c.OrderCount > 1),
            AverageOrderValue = sales.Any() ? sales.Average(s => s.Total) : 0,
            TotalCustomerRevenue = totalRevenue,
            TopCustomers = customerSales.Take(10).ToList(),
            Segments = segments
        };
    }

    public async Task<ExpiryWastageReportDto> GetExpiryWastageReport(int? storeId = null)
    {
        var today = DateTime.Now;

        var allInventory = await _inventoryRepo.FindByCondition(si => si.QuantityOnHand > 0 && (!storeId.HasValue || si.Store_ID == storeId.Value))
            .Include(si => si.ProductBatch)
                .ThenInclude(pb => pb.Product)
            .ToListAsync();

        var expiredItems = allInventory
            .Where(si => si.ProductBatch.ExpiryDate < today)
            .Select(si => new ExpiredItemDetailDto
            {
                ProductBatchID = si.ProductBatch_ID,
                ProductName = si.ProductBatch.Product?.ProductName ?? "Unknown",
                BatchNumber = si.ProductBatch.BatchNumber,
                ExpiryDate = si.ProductBatch.ExpiryDate,
                Quantity = si.QuantityOnHand,
                UnitCost = si.ProductBatch.CostPrice,
                TotalValue = si.QuantityOnHand * si.ProductBatch.CostPrice,
                DaysExpired = (int)(today - si.ProductBatch.ExpiryDate).TotalDays
            })
            .OrderByDescending(e => e.DaysExpired)
            .ToList();

        Func<int, List<NearExpiryItemDto>> getNearExpiry = (days) => allInventory
            .Where(si => si.ProductBatch.ExpiryDate >= today && si.ProductBatch.ExpiryDate <= today.AddDays(days))
            .Select(si => new NearExpiryItemDto
            {
                ProductBatchID = si.ProductBatch_ID,
                ProductName = si.ProductBatch.Product?.ProductName ?? "Unknown",
                BatchNumber = si.ProductBatch.BatchNumber,
                ExpiryDate = si.ProductBatch.ExpiryDate,
                Quantity = si.QuantityOnHand,
                UnitCost = si.ProductBatch.CostPrice,
                TotalValue = si.QuantityOnHand * si.ProductBatch.CostPrice,
                DaysUntilExpiry = (int)(si.ProductBatch.ExpiryDate - today).TotalDays
            })
            .OrderBy(e => e.DaysUntilExpiry)
            .ToList();

        var near30 = getNearExpiry(30);
        var near60 = getNearExpiry(60);
        var near90 = getNearExpiry(90);

        return new ExpiryWastageReportDto
        {
            TotalExpiredValue = expiredItems.Sum(e => e.TotalValue),
            ExpiredItemCount = expiredItems.Count,
            NearExpiryValue = near90.Sum(e => e.TotalValue),
            NearExpiryCount = near90.Count,
            ExpiredItems = expiredItems,
            NearExpiryItems30Days = near30,
            NearExpiryItems60Days = near60,
            NearExpiryItems90Days = near90
        };
    }
}
