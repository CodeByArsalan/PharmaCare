using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces.Inventory;
using PharmaCare.Domain.Models.Configuration;
using PharmaCare.Domain.Models.Inventory;
using PharmaCare.Domain.Models.Products;
using PharmaCare.Infrastructure.Interfaces;

namespace PharmaCare.Application.Implementations.Inventory;

public class StockAlertService(
    IRepository<StockAlert> _alertRepo,
    IRepository<Product> _productRepo,
    IRepository<StoreInventory> _inventoryRepo,
    IRepository<ProductBatch> _batchRepo) : IStockAlertService
{
    public async Task GenerateLowStockAlerts()
    {
        // Get all products with reorder levels set
        var products = await _productRepo
            .FindByCondition(p => p.ReorderLevel.HasValue && p.IsActive)
            .Include(p => p.ProductBatches)
                .ThenInclude(pb => pb.StoreInventories)
            .ToListAsync();

        foreach (var product in products)
        {
            if (!product.ReorderLevel.HasValue) continue;

            // Calculate total stock across all batches and stores
            var totalStock = product.ProductBatches
                .SelectMany(pb => pb.StoreInventories)
                .Sum(si => si.QuantityOnHand);

            // Check if stock is below reorder level
            if (totalStock <= product.ReorderLevel.Value)
            {
                // Check if alert already exists
                var existingAlert = await _alertRepo
                    .FindByCondition(a => a.Product_ID == product.ProductID
                        && a.AlertType == "LowStock"
                        && !a.IsResolved)
                    .FirstOrDefaultAsync();

                if (existingAlert == null)
                {
                    var severity = totalStock == 0 ? "Critical" :
                                   totalStock < product.ReorderLevel.Value / 2 ? "High" : "Medium";

                    var alert = new StockAlert
                    {
                        Product_ID = product.ProductID,
                        Store_ID = null, // Global alert
                        AlertType = "LowStock",
                        Severity = severity,
                        Message = $"{product.ProductName} stock is low ({totalStock} units). Reorder level: {product.ReorderLevel.Value}",
                        IsResolved = false,
                        CreatedDate = DateTime.Now,
                        CreatedBy = 0 // System generated
                    };

                    await _alertRepo.Insert(alert);
                }
            }
        }
    }
    public async Task GenerateExpiringStockAlerts(int daysThreshold = 90)
    {
        var thresholdDate = DateTime.Now.AddDays(daysThreshold);

        // Get batches expiring within threshold
        var expiringBatches = await _batchRepo
            .FindByCondition(pb => pb.ExpiryDate <= thresholdDate
                && pb.ExpiryDate > DateTime.Now)
            .Include(pb => pb.Product)
            .Include(pb => pb.StoreInventories)
            .ToListAsync();

        foreach (var batch in expiringBatches)
        {
            var totalStock = batch.StoreInventories.Sum(si => si.QuantityOnHand);

            // Only alert if there's stock
            if (totalStock > 0)
            {
                // Check if alert already exists
                var existingAlert = await _alertRepo
                    .FindByCondition(a => a.Product_ID == batch.Product_ID
                        && a.AlertType == "Expiring"
                        && a.Message.Contains(batch.BatchNumber)
                        && !a.IsResolved)
                    .FirstOrDefaultAsync();

                if (existingAlert == null)
                {
                    var daysUntilExpiry = (int)(batch.ExpiryDate - DateTime.Now).TotalDays;
                    var severity = daysUntilExpiry <= 30 ? "Critical" :
                                   daysUntilExpiry <= 60 ? "High" : "Medium";

                    var alert = new StockAlert
                    {
                        Product_ID = batch.Product_ID,
                        Store_ID = null,
                        AlertType = "Expiring",
                        Severity = severity,
                        Message = $"{batch.Product?.ProductName} (Batch: {batch.BatchNumber}) expires in {daysUntilExpiry} days. Stock: {totalStock} units",
                        IsResolved = false,
                        CreatedDate = DateTime.Now,
                        CreatedBy = 0
                    };

                    await _alertRepo.Insert(alert);
                }
            }
        }
    }
    public async Task<List<StockAlert>> GetActiveAlerts(int? storeId = null)
    {
        var query = _alertRepo.FindByCondition(a => !a.IsResolved);

        if (storeId.HasValue)
        {
            query = query.Where(a => a.Store_ID == storeId.Value || a.Store_ID == null);
        }

        return await query
            .Include(a => a.Product)
            .Include(a => a.Store)
            .OrderByDescending(a => a.Severity)
            .ThenByDescending(a => a.CreatedDate)
            .ToListAsync();
    }
    public async Task<List<StockAlert>> GetAlertsByType(string alertType, int? storeId = null)
    {
        var query = _alertRepo.FindByCondition(a => !a.IsResolved && a.AlertType == alertType);

        if (storeId.HasValue)
        {
            query = query.Where(a => a.Store_ID == storeId.Value || a.Store_ID == null);
        }

        return await query
            .Include(a => a.Product)
            .Include(a => a.Store)
            .OrderByDescending(a => a.CreatedDate)
            .ToListAsync();
    }
    public async Task<bool> ResolveAlert(int alertId, int userId)
    {
        var alert = _alertRepo.GetById(alertId);
        if (alert == null) return false;

        alert.IsResolved = true;
        alert.ResolvedDate = DateTime.Now;
        alert.ResolvedBy = userId;
        alert.UpdatedDate = DateTime.Now;
        alert.UpdatedBy = userId;

        await _alertRepo.Update(alert);
        return true;
    }
    public async Task<int> GetActiveAlertCount(int? storeId = null)
    {
        var query = _alertRepo.FindByCondition(a => !a.IsResolved);

        if (storeId.HasValue)
        {
            query = query.Where(a => a.Store_ID == storeId.Value || a.Store_ID == null);
        }

        return await query.CountAsync();
    }
}
