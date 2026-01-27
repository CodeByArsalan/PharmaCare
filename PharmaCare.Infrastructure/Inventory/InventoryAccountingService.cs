using Microsoft.EntityFrameworkCore;
using PharmaCare.Domain.Models.AccountManagement;
using PharmaCare.Domain.Models.Inventory;
using PharmaCare.Domain.Models.PurchaseManagement;
using PharmaCare.Infrastructure.Interfaces;
using PharmaCare.Infrastructure.Interfaces.Accounting;
using PharmaCare.Infrastructure.Interfaces.Inventory;

namespace PharmaCare.Infrastructure.Inventory;

/// <summary>
/// Central service for atomic inventory + accounting operations.
/// 
/// CRITICAL RULES:
/// 1. ALL inventory operations MUST go through this service
/// 2. StockMovement is ALWAYS linked to JournalEntry
/// 3. FIFO costing is enforced for all outgoing movements
/// 4. Transaction is atomic - both succeed or both fail
/// </summary>
public sealed class InventoryAccountingService : IInventoryAccountingService
{
    private readonly PharmaCareDBContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJournalPostingEngine _postingEngine;
    private readonly FifoCostCalculator _fifo;

    // Standard Account Type IDs - MUST match AccountType table in database
    private const int INVENTORY_ACCOUNT_TYPE = 6; // Inventory (AccountTypeID=6)
    private const int COGS_ACCOUNT_TYPE = 7; // Consumptions (AccountTypeID=7)
    private const int SALES_ACCOUNT_TYPE = 8; // Sale Account (AccountTypeID=8)
    private const int SUPPLIER_ACCOUNT_TYPE = 4; // Supplier (AccountTypeID=4)
    private const int ADJUSTMENT_ACCOUNT_TYPE = 7; // Use Consumptions for adjustments

    public InventoryAccountingService(
        PharmaCareDBContext context,
        IUnitOfWork unitOfWork,
        IJournalPostingEngine postingEngine,
        FifoCostCalculator fifo)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _postingEngine = postingEngine ?? throw new ArgumentNullException(nameof(postingEngine));
        _fifo = fifo ?? throw new ArgumentNullException(nameof(fifo));
    }

    #region Sale Processing

    public async Task<InventoryAccountingResult> ProcessSaleAsync(
        int storeId,
        int saleId,
        DateTime saleDate,
        IEnumerable<SaleLineDto> saleLines,
        int paymentAccountId,
        int userId,
        CancellationToken ct = default)
    {
        var result = new InventoryAccountingResult();
        var movements = new List<StockMovement>();
        decimal totalCogs = 0;
        decimal totalRevenue = 0;

        try
        {
            await _unitOfWork.BeginTransactionAsync(ct);

            // Get account IDs
            var inventoryAccountId = await GetAccountByTypeAsync(INVENTORY_ACCOUNT_TYPE, ct);
            var cogsAccountId = await GetAccountByTypeAsync(COGS_ACCOUNT_TYPE, ct);
            var salesAccountId = await GetAccountByTypeAsync(SALES_ACCOUNT_TYPE, ct);

            var journalLines = new List<JournalEntryLine>();

            foreach (var line in saleLines)
            {
                // Get batch cost
                var batchCost = await _fifo.GetBatchCostAsync(line.ProductBatchId, line.Quantity, ct);
                totalCogs += batchCost.TotalCost;
                totalRevenue += line.LineTotal;

                // Create stock movement (decrease inventory)
                var movement = new StockMovement
                {
                    Store_ID = storeId,
                    ProductBatch_ID = line.ProductBatchId,
                    MovementType = "SALE",
                    Quantity = -line.Quantity, // Negative for outgoing
                    UnitCost = batchCost.UnitCost,
                    TotalCost = batchCost.TotalCost,
                    ReferenceType = "Sale",
                    ReferenceNumber = $"SALE-{saleId}",
                    ReferenceID = saleId,
                    CreatedBy = userId,
                    CreatedDate = DateTime.Now
                };
                movements.Add(movement);

                // Update store inventory
                await UpdateStoreInventoryAsync(storeId, line.ProductBatchId, -line.Quantity, ct);
            }

            // Add stock movements to context
            foreach (var movement in movements)
            {
                _context.StockMovements.Add(movement);
            }
            await _unitOfWork.SaveChangesAsync(ct);

            // Create journal entry lines
            // DR: COGS
            journalLines.Add(new JournalEntryLine
            {
                Account_ID = cogsAccountId,
                DebitAmount = totalCogs,
                CreditAmount = 0,
                Description = $"Cost of goods sold - Sale {saleId}",
                Store_ID = storeId
            });

            // CR: Inventory
            journalLines.Add(new JournalEntryLine
            {
                Account_ID = inventoryAccountId,
                DebitAmount = 0,
                CreditAmount = totalCogs,
                Description = $"Inventory reduction - Sale {saleId}",
                Store_ID = storeId
            });

            // DR: Cash/Customer (payment account)
            journalLines.Add(new JournalEntryLine
            {
                Account_ID = paymentAccountId,
                DebitAmount = totalRevenue,
                CreditAmount = 0,
                Description = $"Sales receipt - Sale {saleId}",
                Store_ID = storeId
            });

            // CR: Sales Revenue
            journalLines.Add(new JournalEntryLine
            {
                Account_ID = salesAccountId,
                DebitAmount = 0,
                CreditAmount = totalRevenue,
                Description = $"Sales revenue - Sale {saleId}",
                Store_ID = storeId
            });

            // Create and post journal entry
            var journal = await _postingEngine.CreateAndPostAsync(
                entryType: "Sale",
                description: $"Sale Transaction #{saleId}",
                lines: journalLines,
                sourceTable: "Sales",
                sourceId: saleId,
                storeId: storeId,
                userId: userId,
                isSystemEntry: true,
                reference: $"SALE-{saleId}",
                cancellationToken: ct);


            // Link stock movements to journal entry
            foreach (var movement in movements)
            {
                movement.JournalEntry_ID = journal.JournalEntryID;
            }
            await _unitOfWork.SaveChangesAsync(ct);

            await _unitOfWork.CommitAsync(ct);

            result.Success = true;
            result.JournalEntryId = journal.JournalEntryID;
            result.JournalEntryNumber = journal.EntryNumber;
            result.StockMovementIds = movements.Select(m => m.StockMovementID).ToList();
            result.TotalCost = totalCogs;
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    #endregion

    #region Sale Return Processing

    public async Task<InventoryAccountingResult> ProcessSaleReturnAsync(
        int storeId,
        int saleReturnId,
        DateTime returnDate,
        IEnumerable<SaleReturnLineDto> returnLines,
        int paymentAccountId,
        int userId,
        CancellationToken ct = default)
    {
        var result = new InventoryAccountingResult();
        var movements = new List<StockMovement>();
        decimal totalCost = 0;
        decimal totalRevenue = 0;

        try
        {
            await _unitOfWork.BeginTransactionAsync(ct);

            var inventoryAccountId = await GetAccountByTypeAsync(INVENTORY_ACCOUNT_TYPE, ct);
            var cogsAccountId = await GetAccountByTypeAsync(COGS_ACCOUNT_TYPE, ct);
            var salesAccountId = await GetAccountByTypeAsync(SALES_ACCOUNT_TYPE, ct);

            var journalLines = new List<JournalEntryLine>();

            foreach (var line in returnLines)
            {
                totalCost += line.CostTotal;
                totalRevenue += line.PriceTotal;

                // Create stock movement (increase inventory)
                var movement = new StockMovement
                {
                    Store_ID = storeId,
                    ProductBatch_ID = line.ProductBatchId,
                    MovementType = "SALE_RETURN",
                    Quantity = line.Quantity, // Positive for incoming
                    UnitCost = line.UnitCost,
                    TotalCost = line.CostTotal,
                    ReferenceType = "SaleReturn",
                    ReferenceNumber = $"SRET-{saleReturnId}",
                    ReferenceID = saleReturnId,
                    CreatedBy = userId,
                    CreatedDate = DateTime.Now
                };
                movements.Add(movement);

                // Update store inventory (increase)
                await UpdateStoreInventoryAsync(storeId, line.ProductBatchId, line.Quantity, ct);
            }

            foreach (var movement in movements)
            {
                _context.StockMovements.Add(movement);
            }
            await _unitOfWork.SaveChangesAsync(ct);

            // Reverse the original sale entries
            // DR: Inventory
            journalLines.Add(new JournalEntryLine
            {
                Account_ID = inventoryAccountId,
                DebitAmount = totalCost,
                CreditAmount = 0,
                Description = $"Inventory return - Sale Return {saleReturnId}",
                Store_ID = storeId
            });

            // CR: COGS
            journalLines.Add(new JournalEntryLine
            {
                Account_ID = cogsAccountId,
                DebitAmount = 0,
                CreditAmount = totalCost,
                Description = $"COGS reversal - Sale Return {saleReturnId}",
                Store_ID = storeId
            });

            // DR: Sales Revenue
            journalLines.Add(new JournalEntryLine
            {
                Account_ID = salesAccountId,
                DebitAmount = totalRevenue,
                CreditAmount = 0,
                Description = $"Sales reversal - Sale Return {saleReturnId}",
                Store_ID = storeId
            });

            // CR: Cash/Customer
            journalLines.Add(new JournalEntryLine
            {
                Account_ID = paymentAccountId,
                DebitAmount = 0,
                CreditAmount = totalRevenue,
                Description = $"Refund - Sale Return {saleReturnId}",
                Store_ID = storeId
            });

            var journal = await _postingEngine.CreateAndPostAsync(
                entryType: "SaleReturn",
                description: $"Sale Return #{saleReturnId}",
                lines: journalLines,
                sourceTable: "SaleReturns",
                sourceId: saleReturnId,
                storeId: storeId,
                userId: userId,
                isSystemEntry: true,
                reference: $"SRET-{saleReturnId}",
                cancellationToken: ct);

            foreach (var movement in movements)
            {
                movement.JournalEntry_ID = journal.JournalEntryID;
            }
            await _unitOfWork.SaveChangesAsync(ct);

            await _unitOfWork.CommitAsync(ct);

            result.Success = true;
            result.JournalEntryId = journal.JournalEntryID;
            result.JournalEntryNumber = journal.EntryNumber;
            result.StockMovementIds = movements.Select(m => m.StockMovementID).ToList();
            result.TotalCost = totalCost;
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    #endregion

    #region Purchase Processing

    public async Task<InventoryAccountingResult> ProcessPurchaseAsync(
        int storeId,
        int grnId,
        DateTime grnDate,
        IEnumerable<GrnLineDto> grnLines,
        int supplierId,
        int userId,
        CancellationToken ct = default)
    {
        var result = new InventoryAccountingResult();
        var movements = new List<StockMovement>();
        decimal totalCost = 0;

        try
        {
            await _unitOfWork.BeginTransactionAsync(ct);

            var inventoryAccountId = await GetAccountByTypeAsync(INVENTORY_ACCOUNT_TYPE, ct);
            var supplierAccountId = await GetSupplierAccountAsync(supplierId, ct);

            var journalLines = new List<JournalEntryLine>();

            foreach (var line in grnLines)
            {
                totalCost += line.LineTotal;

                // Create stock movement (increase inventory)
                var movement = new StockMovement
                {
                    Store_ID = storeId,
                    ProductBatch_ID = line.ProductBatchId,
                    MovementType = "PURCHASE",
                    Quantity = line.Quantity, // Positive for incoming
                    UnitCost = line.UnitCost,
                    TotalCost = line.LineTotal,
                    ReferenceType = "GRN",
                    ReferenceNumber = $"GRN-{grnId}",
                    ReferenceID = grnId,
                    CreatedBy = userId,
                    CreatedDate = DateTime.Now
                };
                movements.Add(movement);

                // Update store inventory (increase)
                await UpdateStoreInventoryAsync(storeId, line.ProductBatchId, line.Quantity, ct);
            }

            foreach (var movement in movements)
            {
                _context.StockMovements.Add(movement);
            }
            await _unitOfWork.SaveChangesAsync(ct);

            // DR: Inventory
            journalLines.Add(new JournalEntryLine
            {
                Account_ID = inventoryAccountId,
                DebitAmount = totalCost,
                CreditAmount = 0,
                Description = $"Inventory received - GRN {grnId}",
                Store_ID = storeId
            });

            // CR: Accounts Payable (Supplier)
            journalLines.Add(new JournalEntryLine
            {
                Account_ID = supplierAccountId,
                DebitAmount = 0,
                CreditAmount = totalCost,
                Description = $"Payable to supplier - GRN {grnId}",
                Store_ID = storeId
            });

            var journal = await _postingEngine.CreateAndPostAsync(
                entryType: "Purchase",
                description: $"GRN #{grnId}",
                lines: journalLines,
                sourceTable: "GRNs",
                sourceId: grnId,
                storeId: storeId,
                userId: userId,
                isSystemEntry: true,
                reference: $"GRN-{grnId}",
                cancellationToken: ct);


            foreach (var movement in movements)
            {
                movement.JournalEntry_ID = journal.JournalEntryID;
            }
            await _unitOfWork.SaveChangesAsync(ct);

            await _unitOfWork.CommitAsync(ct);

            result.Success = true;
            result.JournalEntryId = journal.JournalEntryID;
            result.JournalEntryNumber = journal.EntryNumber;
            result.StockMovementIds = movements.Select(m => m.StockMovementID).ToList();
            result.TotalCost = totalCost;
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            result.Success = false;
            // Get innermost exception for detailed error message
            var innerEx = ex;
            while (innerEx.InnerException != null)
            {
                innerEx = innerEx.InnerException;
            }
            result.ErrorMessage = $"{ex.Message} | Inner: {innerEx.Message}";
        }

        return result;
    }

    #endregion

    #region Purchase Return Processing

    public async Task<InventoryAccountingResult> ProcessPurchaseReturnAsync(
        int storeId,
        int purchaseReturnId,
        DateTime returnDate,
        IEnumerable<PurchaseReturnLineDto> returnLines,
        int supplierId,
        int userId,
        CancellationToken ct = default)
    {
        var result = new InventoryAccountingResult();
        var movements = new List<StockMovement>();
        decimal totalCost = 0;

        try
        {
            await _unitOfWork.BeginTransactionAsync(ct);

            var inventoryAccountId = await GetAccountByTypeAsync(INVENTORY_ACCOUNT_TYPE, ct);
            var supplierAccountId = await GetSupplierAccountAsync(supplierId, ct);

            var journalLines = new List<JournalEntryLine>();

            foreach (var line in returnLines)
            {
                totalCost += line.LineTotal;

                var movement = new StockMovement
                {
                    Store_ID = storeId,
                    ProductBatch_ID = line.ProductBatchId,
                    MovementType = "PURCHASE_RETURN",
                    Quantity = -line.Quantity, // Negative for outgoing
                    UnitCost = line.UnitCost,
                    TotalCost = line.LineTotal,
                    ReferenceType = "PurchaseReturn",
                    ReferenceNumber = $"PRET-{purchaseReturnId}",
                    ReferenceID = purchaseReturnId,
                    CreatedBy = userId,
                    CreatedDate = DateTime.Now
                };
                movements.Add(movement);

                await UpdateStoreInventoryAsync(storeId, line.ProductBatchId, -line.Quantity, ct);
            }

            foreach (var movement in movements)
            {
                _context.StockMovements.Add(movement);
            }
            await _unitOfWork.SaveChangesAsync(ct);

            // DR: Accounts Payable (reduce liability)
            journalLines.Add(new JournalEntryLine
            {
                Account_ID = supplierAccountId,
                DebitAmount = totalCost,
                CreditAmount = 0,
                Description = $"Reduce payable - Purchase Return {purchaseReturnId}",
                Store_ID = storeId
            });

            // CR: Inventory
            journalLines.Add(new JournalEntryLine
            {
                Account_ID = inventoryAccountId,
                DebitAmount = 0,
                CreditAmount = totalCost,
                Description = $"Inventory return - Purchase Return {purchaseReturnId}",
                Store_ID = storeId
            });

            var journal = await _postingEngine.CreateAndPostAsync(
                entryType: "PurchaseReturn",
                description: $"Purchase Return #{purchaseReturnId}",
                lines: journalLines,
                sourceTable: "PurchaseReturns",
                sourceId: purchaseReturnId,
                storeId: storeId,
                userId: userId,
                isSystemEntry: true,
                reference: $"PRET-{purchaseReturnId}",
                cancellationToken: ct);

            foreach (var movement in movements)
            {
                movement.JournalEntry_ID = journal.JournalEntryID;
            }
            await _unitOfWork.SaveChangesAsync(ct);

            await _unitOfWork.CommitAsync(ct);

            result.Success = true;
            result.JournalEntryId = journal.JournalEntryID;
            result.JournalEntryNumber = journal.EntryNumber;
            result.StockMovementIds = movements.Select(m => m.StockMovementID).ToList();
            result.TotalCost = totalCost;
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    #endregion

    #region Adjustment Processing

    public async Task<InventoryAccountingResult> ProcessAdjustmentAsync(
        int storeId,
        int adjustmentId,
        DateTime adjustmentDate,
        IEnumerable<StockAdjustmentLineDto> adjustmentLines,
        int userId,
        CancellationToken ct = default)
    {
        var result = new InventoryAccountingResult();
        var movements = new List<StockMovement>();
        decimal totalIncrease = 0;
        decimal totalDecrease = 0;

        try
        {
            await _unitOfWork.BeginTransactionAsync(ct);

            var inventoryAccountId = await GetAccountByTypeAsync(INVENTORY_ACCOUNT_TYPE, ct);
            var adjustmentAccountId = await GetAccountByTypeAsync(ADJUSTMENT_ACCOUNT_TYPE, ct);

            var journalLines = new List<JournalEntryLine>();

            foreach (var line in adjustmentLines)
            {
                var movement = new StockMovement
                {
                    Store_ID = storeId,
                    ProductBatch_ID = line.ProductBatchId,
                    MovementType = line.QuantityChange > 0 ? "ADJ_INCREASE" : "ADJ_DECREASE",
                    Quantity = line.QuantityChange,
                    UnitCost = line.UnitCost,
                    TotalCost = line.TotalCost,
                    ReferenceType = "Adjustment",
                    ReferenceNumber = $"ADJ-{adjustmentId}",
                    ReferenceID = adjustmentId,
                    CreatedBy = userId,
                    CreatedDate = DateTime.Now
                };
                movements.Add(movement);

                await UpdateStoreInventoryAsync(storeId, line.ProductBatchId, line.QuantityChange, ct);

                if (line.QuantityChange > 0)
                    totalIncrease += line.TotalCost;
                else
                    totalDecrease += line.TotalCost;
            }

            foreach (var movement in movements)
            {
                _context.StockMovements.Add(movement);
            }
            await _unitOfWork.SaveChangesAsync(ct);

            // Post increase entries
            if (totalIncrease > 0)
            {
                // DR: Inventory, CR: Adjustment (unexplained gain)
                journalLines.Add(new JournalEntryLine
                {
                    Account_ID = inventoryAccountId,
                    DebitAmount = totalIncrease,
                    CreditAmount = 0,
                    Description = $"Inventory increase - Adjustment {adjustmentId}",
                    Store_ID = storeId
                });
                journalLines.Add(new JournalEntryLine
                {
                    Account_ID = adjustmentAccountId,
                    DebitAmount = 0,
                    CreditAmount = totalIncrease,
                    Description = $"Adjustment gain - Adjustment {adjustmentId}",
                    Store_ID = storeId
                });
            }

            // Post decrease entries
            if (totalDecrease > 0)
            {
                // DR: Adjustment (loss), CR: Inventory
                journalLines.Add(new JournalEntryLine
                {
                    Account_ID = adjustmentAccountId,
                    DebitAmount = totalDecrease,
                    CreditAmount = 0,
                    Description = $"Adjustment loss - Adjustment {adjustmentId}",
                    Store_ID = storeId
                });
                journalLines.Add(new JournalEntryLine
                {
                    Account_ID = inventoryAccountId,
                    DebitAmount = 0,
                    CreditAmount = totalDecrease,
                    Description = $"Inventory decrease - Adjustment {adjustmentId}",
                    Store_ID = storeId
                });
            }

            var journal = await _postingEngine.CreateAndPostAsync(
                entryType: "Adjustment",
                description: $"Stock Adjustment #{adjustmentId}",
                lines: journalLines,
                sourceTable: "StockAdjustments",
                sourceId: adjustmentId,
                storeId: storeId,
                userId: userId,
                isSystemEntry: true,
                reference: $"ADJ-{adjustmentId}",
                cancellationToken: ct);

            foreach (var movement in movements)
            {
                movement.JournalEntry_ID = journal.JournalEntryID;
            }
            await _unitOfWork.SaveChangesAsync(ct);

            await _unitOfWork.CommitAsync(ct);

            result.Success = true;
            result.JournalEntryId = journal.JournalEntryID;
            result.JournalEntryNumber = journal.EntryNumber;
            result.StockMovementIds = movements.Select(m => m.StockMovementID).ToList();
            result.TotalCost = totalIncrease + totalDecrease;
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    #endregion

    #region Transfer Processing

    public async Task<InventoryAccountingResult> ProcessTransferAsync(
        int sourceStoreId,
        int destinationStoreId,
        int transferId,
        DateTime transferDate,
        IEnumerable<TransferLineDto> transferLines,
        int userId,
        CancellationToken ct = default)
    {
        var result = new InventoryAccountingResult();
        var movements = new List<StockMovement>();
        decimal totalCost = 0;

        try
        {
            await _unitOfWork.BeginTransactionAsync(ct);

            var inventoryAccountId = await GetAccountByTypeAsync(INVENTORY_ACCOUNT_TYPE, ct);

            foreach (var line in transferLines)
            {
                totalCost += line.TotalCost;

                // Create outgoing movement from source store
                var movementOut = new StockMovement
                {
                    Store_ID = sourceStoreId,
                    ProductBatch_ID = line.ProductBatchId,
                    MovementType = "TRANSFER_OUT",
                    Quantity = -line.Quantity,
                    UnitCost = line.UnitCost,
                    TotalCost = line.TotalCost,
                    ReferenceType = "Transfer",
                    ReferenceNumber = $"TRF-{transferId}",
                    ReferenceID = transferId,
                    CreatedBy = userId,
                    CreatedDate = DateTime.Now
                };

                // Create incoming movement to destination store
                var movementIn = new StockMovement
                {
                    Store_ID = destinationStoreId,
                    ProductBatch_ID = line.ProductBatchId,
                    MovementType = "TRANSFER_IN",
                    Quantity = line.Quantity,
                    UnitCost = line.UnitCost,
                    TotalCost = line.TotalCost,
                    ReferenceType = "Transfer",
                    ReferenceNumber = $"TRF-{transferId}",
                    ReferenceID = transferId,
                    CreatedBy = userId,
                    CreatedDate = DateTime.Now
                };

                movements.Add(movementOut);
                movements.Add(movementIn);

                // Update store inventories
                await UpdateStoreInventoryAsync(sourceStoreId, line.ProductBatchId, -line.Quantity, ct);
                await UpdateStoreInventoryAsync(destinationStoreId, line.ProductBatchId, line.Quantity, ct);
            }

            foreach (var movement in movements)
            {
                _context.StockMovements.Add(movement);
            }
            await _unitOfWork.SaveChangesAsync(ct);

            // Link related movements
            for (int i = 0; i < movements.Count; i += 2)
            {
                movements[i].RelatedMovement_ID = movements[i + 1].StockMovementID;
                movements[i + 1].RelatedMovement_ID = movements[i].StockMovementID;
            }
            await _unitOfWork.SaveChangesAsync(ct);

            // For transfers, we don't post journal entries (just inventory reallocation)
            // unless store-specific sub-ledgers are needed

            // Create inter-store transfer journal (optional)
            var journalLines = new List<JournalEntryLine>
            {
                new JournalEntryLine
                {
                    Account_ID = inventoryAccountId,
                    DebitAmount = totalCost,
                    CreditAmount = 0,
                    Description = $"Transfer to Store {destinationStoreId} - Transfer {transferId}",
                    Store_ID = destinationStoreId
                },
                new JournalEntryLine
                {
                    Account_ID = inventoryAccountId,
                    DebitAmount = 0,
                    CreditAmount = totalCost,
                    Description = $"Transfer from Store {sourceStoreId} - Transfer {transferId}",
                    Store_ID = sourceStoreId
                }
            };

            var journal = await _postingEngine.CreateAndPostAsync(
                entryType: "Transfer",
                description: $"Stock Transfer #{transferId} (Store {sourceStoreId} → {destinationStoreId})",
                lines: journalLines,
                sourceTable: "StockTransfers",
                sourceId: transferId,
                storeId: null, // Multi-store
                userId: userId,
                isSystemEntry: true,
                reference: $"TRF-{transferId}",
                cancellationToken: ct);

            foreach (var movement in movements)
            {
                movement.JournalEntry_ID = journal.JournalEntryID;
            }
            await _unitOfWork.SaveChangesAsync(ct);

            await _unitOfWork.CommitAsync(ct);

            result.Success = true;
            result.JournalEntryId = journal.JournalEntryID;
            result.JournalEntryNumber = journal.EntryNumber;
            result.StockMovementIds = movements.Select(m => m.StockMovementID).ToList();
            result.TotalCost = totalCost;
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    #endregion

    #region Write-Off Processing

    public async Task<InventoryAccountingResult> ProcessWriteOffAsync(
        int storeId,
        int writeOffId,
        DateTime writeOffDate,
        IEnumerable<WriteOffLineDto> writeOffLines,
        int userId,
        CancellationToken ct = default)
    {
        var result = new InventoryAccountingResult();
        var movements = new List<StockMovement>();
        decimal totalCost = 0;

        try
        {
            await _unitOfWork.BeginTransactionAsync(ct);

            var inventoryAccountId = await GetAccountByTypeAsync(INVENTORY_ACCOUNT_TYPE, ct);
            var adjustmentAccountId = await GetAccountByTypeAsync(ADJUSTMENT_ACCOUNT_TYPE, ct);

            var journalLines = new List<JournalEntryLine>();

            foreach (var line in writeOffLines)
            {
                totalCost += line.TotalCost;

                var movement = new StockMovement
                {
                    Store_ID = storeId,
                    ProductBatch_ID = line.ProductBatchId,
                    MovementType = "WRITE_OFF",
                    Quantity = -line.Quantity,
                    UnitCost = line.UnitCost,
                    TotalCost = line.TotalCost,
                    ReferenceType = "WriteOff",
                    ReferenceNumber = $"WO-{writeOffId}",
                    ReferenceID = writeOffId,
                    CreatedBy = userId,
                    CreatedDate = DateTime.Now
                };
                movements.Add(movement);

                await UpdateStoreInventoryAsync(storeId, line.ProductBatchId, -line.Quantity, ct);
            }

            foreach (var movement in movements)
            {
                _context.StockMovements.Add(movement);
            }
            await _unitOfWork.SaveChangesAsync(ct);

            // DR: Write-off expense (loss)
            journalLines.Add(new JournalEntryLine
            {
                Account_ID = adjustmentAccountId,
                DebitAmount = totalCost,
                CreditAmount = 0,
                Description = $"Inventory write-off - WriteOff {writeOffId}",
                Store_ID = storeId
            });

            // CR: Inventory
            journalLines.Add(new JournalEntryLine
            {
                Account_ID = inventoryAccountId,
                DebitAmount = 0,
                CreditAmount = totalCost,
                Description = $"Inventory reduction - WriteOff {writeOffId}",
                Store_ID = storeId
            });

            var journal = await _postingEngine.CreateAndPostAsync(
                entryType: "WriteOff",
                description: $"Inventory Write-Off #{writeOffId}",
                lines: journalLines,
                sourceTable: "WriteOffs",
                sourceId: writeOffId,
                storeId: storeId,
                userId: userId,
                isSystemEntry: true,
                cancellationToken: ct);

            foreach (var movement in movements)
            {
                movement.JournalEntry_ID = journal.JournalEntryID;
            }
            await _unitOfWork.SaveChangesAsync(ct);

            await _unitOfWork.CommitAsync(ct);

            result.Success = true;
            result.JournalEntryId = journal.JournalEntryID;
            result.JournalEntryNumber = journal.EntryNumber;
            result.StockMovementIds = movements.Select(m => m.StockMovementID).ToList();
            result.TotalCost = totalCost;
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    #endregion

    #region Helper Methods

    private async Task<int> GetAccountByTypeAsync(int accountTypeId, CancellationToken ct)
    {
        var account = await _context.ChartOfAccounts
            .AsNoTracking()
            .Where(a => a.AccountType_ID == accountTypeId && a.IsActive)
            .FirstOrDefaultAsync(ct);

        if (account == null)
        {
            throw new InvalidOperationException($"No active account found for AccountType {accountTypeId}");
        }

        return account.AccountID;
    }

    private async Task<int> GetSupplierAccountAsync(int supplierId, CancellationToken ct)
    {
        // First try to find supplier-specific account
        var supplierAccount = await _context.ChartOfAccounts
            .AsNoTracking()
            .Where(a => a.AccountType_ID == SUPPLIER_ACCOUNT_TYPE && a.IsActive)
            .FirstOrDefaultAsync(ct);

        if (supplierAccount == null)
        {
            throw new InvalidOperationException($"No supplier account found");
        }

        return supplierAccount.AccountID;
    }

    private async Task UpdateStoreInventoryAsync(
        int storeId,
        int productBatchId,
        decimal quantityChange,
        CancellationToken ct)
    {
        var inventory = await _context.StoreInventories
            .FirstOrDefaultAsync(si =>
                si.Store_ID == storeId &&
                si.ProductBatch_ID == productBatchId, ct);

        if (inventory == null)
        {
            // Create new inventory record
            inventory = new StoreInventory
            {
                Store_ID = storeId,
                ProductBatch_ID = productBatchId,
                QuantityOnHand = quantityChange
            };
            _context.StoreInventories.Add(inventory);
        }
        else
        {
            inventory.QuantityOnHand += quantityChange;
        }
    }

    #endregion
}


