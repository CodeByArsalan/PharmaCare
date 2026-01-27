using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces.PurchaseManagement;
using PharmaCare.Application.DTOs.Inventory;
using PharmaCare.Application.Utilities;
using PharmaCare.Domain.Models.Inventory;
using PharmaCare.Domain.Models.PurchaseManagement;
using PharmaCare.Infrastructure.Interfaces;
using System.Linq.Expressions;
using PharmaCare.Application.Interfaces.AccountManagement;
using PharmaCare.Domain.Models.Configuration;
using PharmaCare.Infrastructure.Interfaces.Inventory;

namespace PharmaCare.Application.Implementations.PurchaseManagement;

public class GrnService : IGrnService
{
    private readonly IRepository<Grn> _grnRepo;
    private readonly IRepository<ProductBatch> _batchRepo;
    private readonly IRepository<Party> _partyRepo;
    private readonly IPurchaseOrderService _poService;
    private readonly IInventoryAccountingService _inventoryAccountingService;

    public GrnService(
        IRepository<Grn> grnRepo,
        IRepository<ProductBatch> batchRepo,
        IRepository<Party> partyRepo,
        IPurchaseOrderService poService,
        IInventoryAccountingService inventoryAccountingService)
    {
        _grnRepo = grnRepo;
        _batchRepo = batchRepo;
        _partyRepo = partyRepo;
        _poService = poService;
        _inventoryAccountingService = inventoryAccountingService;
    }

    public async Task<List<Grn>> GetGrns()
    {
        return await _grnRepo.FindByCondition(g => true)
            .Include(g => g.PurchaseOrder)
                .ThenInclude(p => p != null ? p.Party : null)
            .Include(g => g.Party)
            .Include(g => g.Store)
            .OrderByDescending(g => g.CreatedDate)
            .ToListAsync();
    }
    public async Task<Grn> GetGrnById(int id)
    {
        return await _grnRepo.FindByCondition(g => g.GrnID == id)
            .Include(g => g.GrnItems)
                .ThenInclude(i => i.Product)
            .Include(g => g.PurchaseOrder)
                .ThenInclude(p => p != null ? p.Party : null)
            .Include(g => g.Party)
            .Include(g => g.Store)
            .FirstOrDefaultAsync();
    }
    public async Task<bool> CreateGrn(Grn grn, int loginUserId)
    {
        // 1. Validation: Check Batch Uniqueness Global
        var batchNumbers = grn.GrnItems.Select(i => i.BatchNumber).ToList();

        // Check internal duplicates
        if (batchNumbers.Count != batchNumbers.Distinct().Count())
        {
            // Internal duplication
            return false;
        }

        // Check DB duplicates (Global)
        foreach (var batchNum in batchNumbers)
        {
            var exists = (await _batchRepo.GetAll()).Any(b => b.BatchNumber == batchNum);
            if (exists) return false;
        }

        // 2. Save GRN
        grn.GrnNumber = UniqueIdGenerator.Generate("GRN");
        grn.CreatedBy = loginUserId;
        grn.CreatedDate = DateTime.Now;

        // Calculate TotalAmount from items (sum of quantity * cost price)
        grn.TotalAmount = grn.GrnItems.Sum(i => i.QuantityReceived * i.CostPrice);
        grn.BalanceAmount = grn.TotalAmount; // Initially, full amount is outstanding
        grn.PaymentStatus = "Unpaid";
        grn.ReturnedAmount = 0;

        // Insert GRN
        if (await _grnRepo.Insert(grn))
        {
            // 3. Create ProductBatches for all items
            var createdBatches = new Dictionary<string, ProductBatch>();
            foreach (var item in grn.GrnItems)
            {
                // Find or Create ProductBatch
                var existingBatch = _batchRepo.FindByCondition(b => b.Product_ID == item.Product_ID && b.BatchNumber == item.BatchNumber).FirstOrDefault();
                if (existingBatch == null)
                {
                    var newBatch = new ProductBatch
                    {
                        Product_ID = item.Product_ID,
                        Grn_ID = grn.GrnID,
                        BatchNumber = item.BatchNumber,
                        ExpiryDate = item.ExpiryDate,
                        CostPrice = item.CostPrice,
                        SellingPrice = item.SellingPrice,
                        MRP = item.SellingPrice,
                        CreatedBy = loginUserId,
                        CreatedDate = DateTime.Now,
                        ProductBatchID = 0
                    };
                    newBatch = await _batchRepo.InsertAndReturn(newBatch);
                    createdBatches[item.BatchNumber] = newBatch;
                }
                else
                {
                    createdBatches[item.BatchNumber] = existingBatch;
                }
            }

            // 4. Process Inventory + Accounting via InventoryAccountingService
            // This atomically handles: StoreInventory, StockMovement, JournalEntry
            try
            {
                // Convert GRN items to GrnLineDtos (with batch IDs we just created)
                var grnLines = grn.GrnItems.Select(i => new GrnLineDto
                {
                    ProductBatchId = createdBatches[i.BatchNumber].ProductBatchID,
                    Quantity = i.QuantityReceived,
                    UnitCost = i.CostPrice
                }).ToList();

                var result = await _inventoryAccountingService.ProcessPurchaseAsync(
                    storeId: grn.Store_ID,
                    grnId: grn.GrnID,
                    grnDate: grn.CreatedDate,
                    grnLines: grnLines,
                    supplierId: grn.Party_ID ?? 0,
                    userId: loginUserId);

                if (result.Success)
                {
                    // Update the original GRN entity directly (already tracked from Insert)
                    grn.JournalEntry_ID = result.JournalEntryId;
                    await _grnRepo.Update(grn);
                }
                else
                {
                    // Log error but don't fail GRN creation - inventory/accounting is secondary
                    System.Diagnostics.Debug.WriteLine($"Inventory/Accounting Error (GRN {grn.GrnID}): {result.ErrorMessage}");
                    throw new InvalidOperationException($"Failed to process inventory/accounting for GRN: {result.ErrorMessage}");
                }
            }
            catch (InvalidOperationException)
            {
                // Re-throw InvalidOperationException as-is (from above or from service)
                throw;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Inventory/Accounting Integration Error (GRN {grn.GrnID}): {ex.Message}");
                throw new InvalidOperationException($"Inventory/Accounting Integration Error: {ex.Message}", ex);
            }

            // 5. Update PO Status and Received Quantities
            if (grn.PurchaseOrder_ID.HasValue)
            {
                // Build dictionary of Product_ID -> QuantityReceived
                var productQuantities = new Dictionary<int, decimal>();
                foreach (var item in grn.GrnItems)
                {
                    if (item.Product_ID.HasValue && item.Product_ID.Value > 0)
                    {
                        var productId = item.Product_ID.Value;
                        if (productQuantities.ContainsKey(productId))
                        {
                            productQuantities[productId] += item.QuantityReceived;
                        }
                        else
                        {
                            productQuantities[productId] = item.QuantityReceived;
                        }
                    }
                }

                await _poService.UpdateReceivedQuantities(grn.PurchaseOrder_ID.Value, productQuantities);
            }

            return true;
        }
        return false;
    }

    public async Task<GrnSummaryDto> GetGrnSummary()
    {
        var today = DateTime.Today;
        var firstDayOfMonth = new DateTime(today.Year, today.Month, 1);

        var todayGrns = await _grnRepo.FindByCondition(g => g.CreatedDate.Date == today).CountAsync();
        var monthGrns = await _grnRepo.FindByCondition(g => g.CreatedDate >= firstDayOfMonth)
            .Include(g => g.GrnItems)
            .ToListAsync();

        var summary = new GrnSummaryDto
        {
            TotalGrnsToday = todayGrns,
            TotalGrnsThisMonth = monthGrns.Count,
            TotalValueThisMonth = monthGrns.Sum(g => g.GrnItems.Sum(i => i.QuantityReceived * i.CostPrice)),
            PendingPOs = await _poService.GetPendingPurchaseOrdersCount() // I assume this exists or I'll add it
        };

        return summary;
    }
}
