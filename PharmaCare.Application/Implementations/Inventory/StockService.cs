using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces.Inventory;
using PharmaCare.Application.DTOs.Inventory;
using PharmaCare.Domain.Models.Inventory;
using PharmaCare.Domain.Models.PurchaseManagement;
using PharmaCare.Domain.Models.Products;
using PharmaCare.Infrastructure.Interfaces;
using PharmaCare.Infrastructure;
using PharmaCare.Application.DTOs.POS;
using PharmaCare.Domain.Models.Configuration;
using PharmaCare.Infrastructure.Interfaces.Inventory;
using PharmaCare.Domain.Models.AccountManagement;
using PharmaCare.Infrastructure.Interfaces.Accounting;

namespace PharmaCare.Application.Implementations.Inventory;

public class StockService : IStockService
{
    private readonly IRepository<StoreInventory> _inventoryRepo;
    private readonly IRepository<Product> _productRepo;
    private readonly IRepository<StockMovement> _movementRepo;
    private readonly IRepository<ProductBatch> _batchRepo;
    private readonly IRepository<StockAdjustment> _adjustmentRepo;
    private readonly IRepository<StockTake> _stockTakeRepo;
    private readonly IRepository<StockTakeItem> _stockTakeItemRepo;
    private readonly IRepository<PurchaseReturn> _returnRepo;
    private readonly IRepository<PurchaseReturnItem> _returnItemRepo;
    private readonly IRepository<StockTransfer> _transferRepo;
    private readonly IRepository<StockTransferItem> _transferItemRepo;
    private readonly PharmaCareDBContext _context;
    private readonly IInventoryAccountingService _inventoryAccountingService;
    private readonly IJournalPostingEngine _postingEngine;

    public StockService(
        IRepository<StoreInventory> inventoryRepo,
        IRepository<Product> productRepo,
        IRepository<StockMovement> movementRepo,
        IRepository<ProductBatch> batchRepo,
        IRepository<StockAdjustment> adjustmentRepo,
        IRepository<StockTake> stockTakeRepo,
        IRepository<StockTakeItem> stockTakeItemRepo,
        IRepository<PurchaseReturn> returnRepo,
        IRepository<PurchaseReturnItem> returnItemRepo,
        IRepository<StockTransfer> transferRepo,
        IRepository<StockTransferItem> transferItemRepo,
        PharmaCareDBContext context,
        IInventoryAccountingService inventoryAccountingService,
        IJournalPostingEngine postingEngine)
    {
        _inventoryRepo = inventoryRepo;
        _productRepo = productRepo;
        _movementRepo = movementRepo;
        _batchRepo = batchRepo;
        _adjustmentRepo = adjustmentRepo;
        _stockTakeRepo = stockTakeRepo;
        _stockTakeItemRepo = stockTakeItemRepo;
        _returnRepo = returnRepo;
        _returnItemRepo = returnItemRepo;
        _transferRepo = transferRepo;
        _transferItemRepo = transferItemRepo;
        _context = context;
        _inventoryAccountingService = inventoryAccountingService;
        _postingEngine = postingEngine;
    }

    //public async Task<List<PurchaseReturn>> GetPurchaseReturns()
    //{
    //    return await _returnRepo.GetAllWithInclude(r => r.Supplier, r => r.Store).ToListAsync();
    //}

    #region Stock Overview
    public async Task<List<StoreInventory>> GetStockOverview()
    {
        return _inventoryRepo.GetAllWithInclude(i => i.ProductBatch, i => i.ProductBatch.Product, i => i.Store).ToList();
    }
    public async Task<List<Product>> GetProducts()
    {
        return (await _productRepo.GetAll()).Where(p => p.IsActive).ToList();
    }
    public async Task<int> GetLowStockItemsCount(int? storeId)
    {
        var query = _inventoryRepo.FindByCondition(i => (!storeId.HasValue || i.Store_ID == storeId.Value))
            .Include(i => i.ProductBatch)
                .ThenInclude(b => b != null ? b.Product : null);

        return await query.CountAsync(i => i.ProductBatch != null && i.ProductBatch.Product != null &&
                                           i.QuantityOnHand <= (i.ProductBatch.Product.ReorderLevel ?? 10));
    }
    public async Task<InventorySummaryDto> GetInventorySummary(int? storeId = null)
    {
        var query = _inventoryRepo.FindByCondition(i => (!storeId.HasValue || i.Store_ID == storeId.Value))
            .Include(i => i.ProductBatch)
                .ThenInclude(b => b.Product);

        var inventory = await query.ToListAsync();

        var summary = new InventorySummaryDto
        {
            TotalProducts = inventory.Where(i => i.ProductBatch != null).Select(i => i.ProductBatch.Product_ID).Distinct().Count(),
            LowStockItems = inventory.Count(i => i.ProductBatch?.Product != null && i.QuantityOnHand <= (i.ProductBatch.Product.ReorderLevel ?? 10)),
            ExpiredItems = inventory.Count(i => i.ProductBatch != null && i.ProductBatch.ExpiryDate <= DateTime.Now),
            ExpiringSoonItems = inventory.Count(i => i.ProductBatch != null && i.ProductBatch.ExpiryDate > DateTime.Now && i.ProductBatch.ExpiryDate <= DateTime.Now.AddMonths(3)),
            TotalInventoryValue = inventory.Sum(i => i.QuantityOnHand * (i.ProductBatch?.CostPrice ?? 0)),
            PendingPurchaseOrders = await _context.Set<PurchaseOrder>().CountAsync(po => po.Status == "Pending" || po.Status == "Partially Received")
        };

        return summary;
    }
    #endregion

    #region Adjustments
    public async Task<List<StockAdjustment>> GetStockAdjustments()
    {
        return await _adjustmentRepo.GetAllWithInclude(x => x.Store, x => x.ProductBatch).ToListAsync();
    }
    public async Task<StockAdjustment> GetStockAdjustmentById(int id)
    {
        return await _adjustmentRepo.FindByCondition(a => a.StockAdjustmentID == id)
            .Include(a => a.Store)
            .Include(a => a.ProductBatch)
                .ThenInclude(b => b.Product)
            .FirstOrDefaultAsync();
    }
    public async Task<bool> AdjustStock(StockAdjustment adjustment)
    {
        // 1. Validation - check negative adjustments don't exceed current stock
        var inventory = _inventoryRepo.FindByCondition(i => i.Store_ID == adjustment.Store_ID && i.ProductBatch_ID == adjustment.ProductBatch_ID).FirstOrDefault();
        if (adjustment.QuantityAdjusted < 0 && (inventory?.QuantityOnHand ?? 0) + adjustment.QuantityAdjusted < 0)
        {
            return false;
        }

        adjustment.CreatedDate = adjustment.AdjustmentDate = DateTime.Now;

        var batch = _batchRepo.GetById(adjustment.ProductBatch_ID);
        if (batch != null)
        {
            adjustment.FinancialImpact = batch.CostPrice * adjustment.QuantityAdjusted;
        }

        // 2. Save the adjustment record
        if (await _adjustmentRepo.Insert(adjustment))
        {
            // 3. Process Inventory + Accounting via InventoryAccountingService
            try
            {
                var adjustmentLines = new List<StockAdjustmentLineDto>
                {
                    new StockAdjustmentLineDto
                    {
                        ProductBatchId = adjustment.ProductBatch_ID,
                        QuantityChange = adjustment.QuantityAdjusted,
                        UnitCost = batch?.CostPrice ?? 0,
                        Reason = adjustment.Reason
                    }
                };

                var result = await _inventoryAccountingService.ProcessAdjustmentAsync(
                    storeId: adjustment.Store_ID,
                    adjustmentId: adjustment.StockAdjustmentID,
                    adjustmentDate: adjustment.AdjustmentDate,
                    adjustmentLines: adjustmentLines,
                    userId: adjustment.AdjustedBy);

                if (!result.Success)
                {
                    Console.WriteLine($"Inventory/Accounting Error (Adjustment): {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Inventory/Accounting Integration Error (Adjustment): {ex.Message}");
            }
            return true;
        }
        return false;
    }
    #endregion

    #region Stock Take
    public async Task<List<StockTake>> GetStockTakes()
    {
        return await _stockTakeRepo.FindByCondition(s => true)
            .Include(s => s.Store)
            .OrderByDescending(s => s.CreatedDate)
            .ToListAsync();
    }
    public async Task<StockTake> GetStockTake(int id)
    {
        return await _stockTakeRepo.FindByCondition(s => s.StockTakeID == id)
            .Include(s => s.StockTakeItems)
                .ThenInclude(i => i.ProductBatch)
                    .ThenInclude(pb => pb.Product)
            .FirstOrDefaultAsync();
    }
    public async Task<StockTake> InitiateStockTake(int storeId, int loginUserId, string remarks, int? categoryId = null)
    {
        // 1. Check if there is any inventory to count
        IQueryable<StoreInventory> query = _inventoryRepo.FindByCondition(i => i.Store_ID == storeId);

        if (categoryId.HasValue)
        {
            query = query
                .Include(i => i.ProductBatch)
                .ThenInclude(pb => pb!.Product)
                .Where(i => i.ProductBatch != null && i.ProductBatch.Product != null && i.ProductBatch.Product.SubCategory_ID == categoryId.Value);
        }

        var inventory = await query.ToListAsync();

        // 2. Create Stock Take Record
        var stockTake = new StockTake
        {
            Store_ID = storeId,
            CreatedBy = loginUserId,
            CreatedDate = DateTime.Now,
            Status = "Open",
            Remarks = remarks
        };

        if (await _stockTakeRepo.Insert(stockTake))
        {
            foreach (var item in inventory)
            {
                var stockTakeItem = new StockTakeItem
                {
                    StockTake_ID = stockTake.StockTakeID,
                    ProductBatch_ID = item.ProductBatch_ID,
                    SystemQuantity = item.QuantityOnHand,
                    PhysicalQuantity = item.QuantityOnHand
                };
                await _stockTakeItemRepo.Insert(stockTakeItem);
            }
            return stockTake;
        }
        return null;
    }

    public async Task<bool> UpdateStockTakeItem(int itemId, decimal physicalQty)
    {
        var item = _stockTakeItemRepo.GetById(itemId);
        if (item == null) return false;
        item.PhysicalQuantity = physicalQty;
        return await _stockTakeItemRepo.Update(item);
    }
    public async Task<bool> CompleteStockTake(int stockTakeId, int loginUserId)
    {
        var stockTake = await GetStockTake(stockTakeId);
        if (stockTake == null || stockTake.Status != "Open") return false;

        foreach (var item in stockTake.StockTakeItems)
        {
            if (item.ProductBatch != null)
            {
                item.VarianceCost = item.Variance * item.ProductBatch.CostPrice;
                await _stockTakeItemRepo.Update(item);
            }

            if (item.Variance != 0)
            {
                var inventory = await _inventoryRepo.FindByCondition(i => i.Store_ID == stockTake.Store_ID && i.ProductBatch_ID == item.ProductBatch_ID).FirstOrDefaultAsync();
                if (inventory != null)
                {
                    inventory.QuantityOnHand = item.PhysicalQuantity;
                    await _inventoryRepo.Update(inventory);
                }

                // TODO: Replace with InventoryAccountingService (Step 5.5)
                var movement = new StockMovement
                {
                    Store_ID = stockTake.Store_ID,
                    ProductBatch_ID = item.ProductBatch_ID,
                    MovementType = item.Variance > 0 ? "ADJ_INCREASE" : "ADJ_DECREASE",
                    Quantity = item.Variance,
                    UnitCost = item.ProductBatch?.CostPrice ?? 0,
                    TotalCost = Math.Abs(item.Variance) * (item.ProductBatch?.CostPrice ?? 0),
                    ReferenceType = "StockTake",
                    ReferenceNumber = $"ST-{stockTake.StockTakeID}",
                    ReferenceID = stockTake.StockTakeID,
                    JournalEntry_ID = null, // TODO: Link after InventoryAccountingService
                    CreatedBy = loginUserId,
                    CreatedDate = DateTime.Now
                };
                await _movementRepo.Insert(movement);
            }
        }

        stockTake.Status = "Completed";
        stockTake.CompletedBy = loginUserId;
        stockTake.CompletedDate = DateTime.Now;
        return await _stockTakeRepo.Update(stockTake);
    }
    #endregion

    #region Purchase Return
    public async Task<List<PurchaseReturn>> GetPurchaseReturns()
    {
        return await _returnRepo.GetAllWithInclude(r => r.Party, r => r.Store, r => r.PurchaseReturnItems)
            .OrderByDescending(r => r.ReturnDate)
            .ToListAsync();
    }
    public async Task<bool> CreatePurchaseReturn(PurchaseReturn purchaseReturn, int loginUserId)
    {
        purchaseReturn.CreatedBy = loginUserId;
        purchaseReturn.CreatedDate = DateTime.Now;
        purchaseReturn.Status = "Pending";

        // Calculate missing totals
        if (purchaseReturn.PurchaseReturnItems != null && purchaseReturn.PurchaseReturnItems.Any())
        {
            foreach (var item in purchaseReturn.PurchaseReturnItems)
            {
                item.TotalLineAmount = item.Quantity * item.UnitPrice;
            }
            purchaseReturn.TotalAmount = purchaseReturn.PurchaseReturnItems.Sum(x => x.TotalLineAmount);
        }

        if (await _returnRepo.Insert(purchaseReturn))
        {
            return true;
        }
        return false;
    }
    public async Task<PurchaseReturn> GetPurchaseReturn(int id)
    {
        return await _returnRepo.FindByCondition(r => r.PurchaseReturnID == id)
            .Include(r => r.PurchaseReturnItems)
                .ThenInclude(i => i.ProductBatch)
                    .ThenInclude(pb => pb.Product)
            .Include(r => r.Party)
            .Include(r => r.Store)
            .FirstOrDefaultAsync();
    }
    public async Task<bool> ApprovePurchaseReturn(int id, int loginUserId)
    {
        var purchaseReturn = await GetPurchaseReturn(id);
        if (purchaseReturn == null || purchaseReturn.Status != "Pending") return false;

        // Validate sufficient stock for all items before proceeding
        foreach (var item in purchaseReturn.PurchaseReturnItems)
        {
            var inventory = await _inventoryRepo.FindByCondition(i => i.Store_ID == purchaseReturn.Store_ID && i.ProductBatch_ID == item.ProductBatch_ID).FirstOrDefaultAsync();
            if (inventory == null || inventory.QuantityOnHand < item.Quantity)
            {
                return false; // Insufficient stock
            }
        }

        // Convert return items to DTOs for InventoryAccountingService
        var returnLines = purchaseReturn.PurchaseReturnItems.Select(item => new PurchaseReturnLineDto
        {
            ProductBatchId = item.ProductBatch_ID,
            Quantity = item.Quantity,
            UnitCost = item.UnitPrice
        }).ToList();

        // Process via InventoryAccountingService for atomic inventory + accounting
        var result = await _inventoryAccountingService.ProcessPurchaseReturnAsync(
            storeId: purchaseReturn.Store_ID,
            purchaseReturnId: purchaseReturn.PurchaseReturnID,
            returnDate: purchaseReturn.ReturnDate,
            returnLines: returnLines,
            supplierId: purchaseReturn.Party_ID,
            userId: loginUserId);

        if (!result.Success)
        {
            Console.WriteLine($"Purchase Return Accounting Error: {result.ErrorMessage}");
            return false;
        }

        // Update purchase return with journal entry reference
        purchaseReturn.JournalEntry_ID = result.JournalEntryId;
        purchaseReturn.Status = "Approved";
        purchaseReturn.UpdatedBy = loginUserId;
        purchaseReturn.UpdatedDate = DateTime.Now;

        // Set initial refund status based on whether supplier was already paid
        // AND update GRN balance to reflect returned goods
        if (purchaseReturn.Grn_ID.HasValue)
        {
            var grn = await _context.Set<Grn>().FirstOrDefaultAsync(g => g.GrnID == purchaseReturn.Grn_ID);
            if (grn != null)
            {
                // Update GRN to track returned goods (preserve original TotalAmount)
                grn.ReturnedAmount += purchaseReturn.TotalAmount;
                grn.BalanceAmount = grn.TotalAmount - grn.AmountPaid - grn.ReturnedAmount;

                // Update payment status based on new balance
                if (grn.BalanceAmount <= 0)
                {
                    grn.PaymentStatus = "Paid";
                    grn.BalanceAmount = 0; // Ensure no negative balance
                }
                else if (grn.AmountPaid > 0)
                {
                    grn.PaymentStatus = "Partial";
                }
                else
                {
                    grn.PaymentStatus = "Unpaid";
                }

                await _context.SaveChangesAsync();

                // Set refund status based on whether supplier was already paid
                if (grn.AmountPaid > 0)
                {
                    // Supplier was already paid, refund may be expected
                    purchaseReturn.RefundStatus = "Pending";
                    purchaseReturn.RefundAmount = purchaseReturn.TotalAmount;
                }
                else
                {
                    // Supplier not yet paid, no refund needed
                    purchaseReturn.RefundStatus = "NotApplicable";
                }
            }
            else
            {
                purchaseReturn.RefundStatus = "NotApplicable";
            }
        }
        else
        {
            purchaseReturn.RefundStatus = "NotApplicable";
        }

        return await _returnRepo.Update(purchaseReturn);
    }
    public async Task<List<ReturnableItemDto>> GetReturnableItems(int partyId, int storeId)
    {
        var inventory = await _context.Set<StoreInventory>().AsNoTracking()
        .Where(i => i.Store_ID == storeId && i.QuantityOnHand > 0)
            .Include(i => i.ProductBatch)
                .ThenInclude(pb => pb.Grn)
                    .ThenInclude(g => g.PurchaseOrder)
            .Include(i => i.ProductBatch)
                .ThenInclude(pb => pb.Product)
        .Where(i => (i.ProductBatch.Grn_ID != null && i.ProductBatch.Grn.PurchaseOrder.Party_ID == partyId) ||
                    (i.ProductBatch.Grn_ID == null && _context.Set<GrnItem>().Any(gi => gi.BatchNumber == i.ProductBatch.BatchNumber && gi.Product_ID == i.ProductBatch.Product_ID && gi.Grn.PurchaseOrder.Party_ID == partyId)))
        .Select(i => new ReturnableItemDto
        {
            ProductBatchID = i.ProductBatch_ID,
            GrnID = i.ProductBatch.Grn_ID,
            BatchNumber = i.ProductBatch.BatchNumber,
            ProductName = i.ProductBatch.Product.ProductName,
            CostPrice = i.ProductBatch.CostPrice,
            QuantityOnHand = i.QuantityOnHand,
            ExpiryDate = i.ProductBatch.ExpiryDate
        })
            .ToListAsync();
        return inventory;
    }

    public async Task<bool> ProcessSupplierRefund(int purchaseReturnId, string refundMethod, int loginUserId)
    {
        var purchaseReturn = await GetPurchaseReturn(purchaseReturnId);
        if (purchaseReturn == null || purchaseReturn.Status != "Approved" || purchaseReturn.RefundStatus != "Pending")
        {
            return false;
        }

        // Account Type IDs matching the application's conventions
        const int CASH_ACCOUNT_TYPE = 1;
        const int BANK_ACCOUNT_TYPE = 2;
        const int SUPPLIER_ACCOUNT_TYPE = 4;

        // Get account IDs for journal entry
        var paymentAccountTypeId = refundMethod == "Bank" ? BANK_ACCOUNT_TYPE : CASH_ACCOUNT_TYPE;

        var supplierAccount = await _context.Set<ChartOfAccount>()
            .FirstOrDefaultAsync(a => a.AccountType_ID == SUPPLIER_ACCOUNT_TYPE && a.IsActive);
        var paymentAccount = await _context.Set<ChartOfAccount>()
            .FirstOrDefaultAsync(a => a.AccountType_ID == paymentAccountTypeId && a.IsActive);

        if (supplierAccount == null || paymentAccount == null)
        {
            Console.WriteLine("Required accounts not found for refund processing");
            return false;
        }

        // Create journal entry lines for refund: DR Cash/Bank, CR Accounts Payable
        var journalLines = new List<JournalEntryLine>
        {
            new JournalEntryLine
            {
                Account_ID = paymentAccount.AccountID,
                DebitAmount = purchaseReturn.RefundAmount,
                CreditAmount = 0,
                Description = $"Refund received via {refundMethod} - PR #{purchaseReturnId}",
                Store_ID = purchaseReturn.Store_ID
            },
            new JournalEntryLine
            {
                Account_ID = supplierAccount.AccountID,
                DebitAmount = 0,
                CreditAmount = purchaseReturn.RefundAmount,
                Description = $"Supplier refund received - PR #{purchaseReturnId}",
                Store_ID = purchaseReturn.Store_ID
            }
        };

        // Use IJournalPostingEngine to create and post entry with proper sequencing
        var journalEntry = await _postingEngine.CreateAndPostAsync(
            entryType: "SupplierRefund",
            description: $"Supplier Refund - Purchase Return #{purchaseReturnId}",
            lines: journalLines,
            sourceTable: "PurchaseReturns",
            sourceId: purchaseReturnId,
            storeId: purchaseReturn.Store_ID,
            userId: loginUserId,
            isSystemEntry: true);

        if (journalEntry == null)
        {
            Console.WriteLine("Failed to create journal entry for supplier refund");
            return false;
        }

        // Update purchase return with refund details
        purchaseReturn.RefundMethod = refundMethod;
        purchaseReturn.RefundStatus = "Received";
        purchaseReturn.RefundJournalEntry_ID = journalEntry.JournalEntryID;
        purchaseReturn.Status = "Completed";
        purchaseReturn.UpdatedBy = loginUserId;
        purchaseReturn.UpdatedDate = DateTime.Now;

        return await _returnRepo.Update(purchaseReturn);
    }

    #endregion

    #region Stock Transfer
    public async Task<List<StockTransfer>> GetStockTransfers()
    {
        return await _transferRepo.GetAllWithInclude(t => t.SourceStore, t => t.DestinationStore)
            .OrderByDescending(t => t.TransferDate)
            .ToListAsync();
    }
    public async Task<StockTransfer> GetStockTransferById(int id)
    {
        return await _transferRepo.FindByCondition(t => t.StockTransferID == id)
            .Include(t => t.SourceStore)
            .Include(t => t.DestinationStore)
            .Include(t => t.StockTransferItems)
                .ThenInclude(i => i.ProductBatch)
                    .ThenInclude(pb => pb.Product)
            .FirstOrDefaultAsync();
    }
    public async Task<bool> CreateStockTransfer(StockTransfer transfer, int loginUserId)
    {
        if (transfer.StockTransferItems == null || !transfer.StockTransferItems.Any()) return false;

        foreach (var item in transfer.StockTransferItems)
        {
            var sourceInventory = await _inventoryRepo
                .FindByCondition(i => i.Store_ID == transfer.SourceStore_ID && i.ProductBatch_ID == item.ProductBatch_ID)
                .FirstOrDefaultAsync();

            if (sourceInventory == null || sourceInventory.QuantityOnHand < item.Quantity) return false;
        }

        transfer.CreatedBy = loginUserId;
        transfer.CreatedDate = DateTime.Now;
        transfer.Status = "Pending";

        await _transferRepo.Insert(transfer);

        foreach (var item in transfer.StockTransferItems)
        {
            var sourceInventory = await _inventoryRepo
                .FindByCondition(i => i.Store_ID == transfer.SourceStore_ID && i.ProductBatch_ID == item.ProductBatch_ID)
                .FirstOrDefaultAsync();

            if (sourceInventory != null)
            {
                sourceInventory.QuantityOnHand -= item.Quantity;
                await _inventoryRepo.Update(sourceInventory);

                // TODO: Replace with InventoryAccountingService (Step 5.5)
                var batch = await _batchRepo.GetByIdAsync(item.ProductBatch_ID);
                var movementOut = new StockMovement
                {
                    Store_ID = transfer.SourceStore_ID,
                    ProductBatch_ID = item.ProductBatch_ID,
                    MovementType = "TRANSFER_OUT",
                    Quantity = -item.Quantity,
                    UnitCost = batch?.CostPrice ?? 0,
                    TotalCost = item.Quantity * (batch?.CostPrice ?? 0),
                    ReferenceType = "Transfer",
                    ReferenceNumber = transfer.TransferNumber,
                    ReferenceID = transfer.StockTransferID,
                    JournalEntry_ID = null, // TODO: Link after InventoryAccountingService
                    CreatedBy = loginUserId,
                    CreatedDate = DateTime.Now
                };
                await _movementRepo.Insert(movementOut);
            }
        }
        return true;
    }
    public async Task<bool> ApproveStockTransfer(int transferId, int loginUserId)
    {
        var transfer = await GetStockTransferById(transferId);
        if (transfer == null || transfer.Status != "Pending") return false;

        foreach (var item in transfer.StockTransferItems)
        {
            var destInventory = await _inventoryRepo
                .FindByCondition(i => i.Store_ID == transfer.DestinationStore_ID && i.ProductBatch_ID == item.ProductBatch_ID)
                .FirstOrDefaultAsync();

            if (destInventory == null)
            {
                destInventory = new StoreInventory
                {
                    Store_ID = transfer.DestinationStore_ID,
                    ProductBatch_ID = item.ProductBatch_ID,
                    QuantityOnHand = 0
                };
                await _inventoryRepo.Insert(destInventory);
            }

            destInventory.QuantityOnHand += item.Quantity;
            await _inventoryRepo.Update(destInventory);

            // TODO: Replace with InventoryAccountingService (Step 5.5)
            var batchIn = await _batchRepo.GetByIdAsync(item.ProductBatch_ID);
            var movementIn = new StockMovement
            {
                Store_ID = transfer.DestinationStore_ID,
                ProductBatch_ID = item.ProductBatch_ID,
                MovementType = "TRANSFER_IN",
                Quantity = item.Quantity,
                UnitCost = batchIn?.CostPrice ?? 0,
                TotalCost = item.Quantity * (batchIn?.CostPrice ?? 0),
                ReferenceType = "Transfer",
                ReferenceNumber = transfer.TransferNumber,
                ReferenceID = transfer.StockTransferID,
                JournalEntry_ID = null, // TODO: Link after InventoryAccountingService
                CreatedBy = loginUserId,
                CreatedDate = DateTime.Now
            };
            await _movementRepo.Insert(movementIn);
        }

        transfer.Status = "Completed";
        transfer.ReceivedBy = loginUserId;
        transfer.ReceivedDate = DateTime.Now;
        transfer.UpdatedBy = loginUserId;
        transfer.UpdatedDate = DateTime.Now;
        await _transferRepo.Update(transfer);

        return true;
    }
    public async Task<List<ReturnableItemDto>> GetTransferableItems(int storeId)
    {
        var inventory = await _inventoryRepo.FindByCondition(i => i.Store_ID == storeId && i.QuantityOnHand > 0)
                                           .Include(i => i.ProductBatch)
                                           .ThenInclude(b => b.Product)
                                           .ToListAsync();

        return inventory.Select(i => new ReturnableItemDto
        {
            ProductBatchID = i.ProductBatch_ID,
            ProductName = i.ProductBatch?.Product?.ProductName ?? "Unknown",
            BatchNumber = i.ProductBatch?.BatchNumber ?? "N/A",
            QuantityOnHand = i.QuantityOnHand,
            CostPrice = i.ProductBatch?.CostPrice ?? 0,
            ExpiryDate = i.ProductBatch?.ExpiryDate ?? DateTime.MinValue
        }).ToList();
    }
    public async Task<List<ProductSearchResultDto>> SearchProductBatchesAsync(string query, int? storeId)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            return new List<ProductSearchResultDto>();

        var queryLower = query.ToLower();

        var products = await _context.Products
            .Include(p => p.ProductBatches)
                .ThenInclude(b => b.StoreInventories)
            .Where(p => p.ProductName.ToLower().Contains(queryLower) ||
                       p.ProductCode.ToLower().Contains(queryLower) ||
                       p.Barcode.Contains(query) ||
                       p.ProductBatches.Any(b => b.BatchNumber.ToLower().Contains(queryLower)))
            .Take(20)
            .ToListAsync();

        return products.Select(p => new ProductSearchResultDto
        {
            ProductID = p.ProductID,
            ProductName = p.ProductName,
            ProductCode = p.ProductCode ?? p.Sku,
            Barcode = p.Barcode,
            AvailableBatches = p.ProductBatches
                .Select(b => new BatchInfoDto
                {
                    ProductBatchID = b.ProductBatchID,
                    BatchNumber = b.BatchNumber,
                    ExpiryDate = b.ExpiryDate,
                    AvailableQuantity = b.StoreInventories
                        .Where(si => !storeId.HasValue || si.Store_ID == storeId.Value)
                        .Sum(si => si.QuantityOnHand),
                    Price = b.CostPrice
                })
                .OrderBy(b => b.ExpiryDate)
                .ToList()
        }).ToList();
    }
    #endregion
}
