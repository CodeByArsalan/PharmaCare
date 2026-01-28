using Microsoft.EntityFrameworkCore;
using PharmaCare.Domain.Models.Configuration;
using PharmaCare.Domain.Models.Inventory;
using PharmaCare.Domain.Models.Products;
using PharmaCare.Infrastructure.Interfaces;

namespace PharmaCare.Infrastructure.Implementations.PointOfSale;

public class PosRepository(PharmaCareDBContext _context) : IPosRepository
{
    public async Task<List<Product>> SearchProductsAsync(string query)
    {
        return await _context.Products
            .Include(p => p.ProductBatches)
                .ThenInclude(b => b.StoreInventories)
            .Where(p => p.ProductName.ToLower().Contains(query.ToLower()) ||
                       p.ProductCode.ToLower().Contains(query.ToLower()) ||
                       p.Barcode.Contains(query))
            .Take(10)
            .ToListAsync();
    }
    public async Task<ProductBatch?> GetBatchDetailsAsync(int productBatchId)
    {
        return await _context.ProductBatches
             .Include(b => b.Product)
             .Include(b => b.StoreInventories)
             .FirstOrDefaultAsync(b => b.ProductBatchID == productBatchId);
    }
    public async Task<decimal> GetBatchStockQuantityAsync(int productBatchId)
    {
        return await _context.StoreInventories
            .Where(si => si.ProductBatch_ID == productBatchId)
            .SumAsync(si => si.QuantityOnHand);
    }
    public async Task<decimal> GetStockQuantityAsync(int productBatchId)
    {
        return await _context.StoreInventories
            .Where(si => si.ProductBatch_ID == productBatchId)
            .SumAsync(si => si.QuantityOnHand);
    }
    public async Task<StockMain?> GetSaleWithDetailsAsync(int stockMainId)
    {
        return await _context.StockMains
            .Include(s => s.Party)
            .Include(s => s.StockDetails)
                .ThenInclude(sd => sd.Product)
            .Include(s => s.StockDetails)
                .ThenInclude(sd => sd.ProductBatch)
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.StockMainID == stockMainId);
    }
    public async Task<Party?> GetPartyByPhoneAsync(string phone)
    {
        return await _context.Parties.FirstOrDefaultAsync(p => p.ContactNumber == phone);
    }
    public async Task<Party?> GetPartyByIdAsync(int id)
    {
        return await _context.Parties.FindAsync(id);
    }
    public void AddParty(Party party)
    {
        _context.Parties.Add(party);
    }
    public void UpdateParty(Party party)
    {
        _context.Parties.Update(party);
    }
    public void UpdateInventory(int productBatchId, decimal quantityChange, int storeId)
    {
        var inventory = _context.StoreInventories
                .IgnoreQueryFilters()
                .FirstOrDefault(si => si.ProductBatch_ID == productBatchId && si.Store_ID == storeId);

        if (inventory != null)
        {
            inventory.QuantityOnHand += quantityChange; // quantityChange should be negative for sales
        }
    }
    
    public void AddStockMovement(int productBatchId, decimal quantityChange, string reason, int storeId, int createdBy, string referenceNumber = "")
    {
        _context.StockMovements.Add(new StockMovement
        {
            Store_ID = storeId,
            ProductBatch_ID = productBatchId,
            MovementType = reason,
            Quantity = quantityChange,
            UnitCost = 0,
            TotalCost = 0,
            ReferenceType = reason,
            ReferenceNumber = referenceNumber,
            ReferenceID = null,

            CreatedBy = createdBy,
            CreatedDate = DateTime.Now
        });
    }
    public async Task<int> SaveChangesAsync()
    {
        try
        {
            return await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            var innerMessage = ex.InnerException?.Message ?? ex.Message;
            System.Diagnostics.Debug.WriteLine($"DB UPDATE ERROR: {innerMessage}");
            throw new Exception($"Database Save Failure: {innerMessage}", ex);
        }
        catch (Exception ex)
        {
            throw new Exception($"General Save Failure: {ex.Message}", ex);
        }
    }
    public async Task<List<StockMain>> GetSalesHistoryAsync(DateTime? startDate, DateTime? endDate, int? storeId = null)
    {
        // Sales are StockMain with InvoiceType_ID = 1 (SALE)
        var query = _context.StockMains
            .Include(s => s.Party)
            .Include(s => s.StockDetails)
            .Where(s => s.InvoiceType_ID == 1) // InvoiceType 1 = SALE
            .AsQueryable();

        if (storeId.HasValue)
        {
            query = query.Where(s => s.Store_ID == storeId.Value);
        }

        if (startDate.HasValue)
        {
            query = query.Where(s => s.InvoiceDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(s => s.InvoiceDate <= endDate.Value.AddDays(1).AddSeconds(-1));
        }

        return await query.OrderByDescending(s => s.InvoiceDate).ToListAsync();
    }
}
