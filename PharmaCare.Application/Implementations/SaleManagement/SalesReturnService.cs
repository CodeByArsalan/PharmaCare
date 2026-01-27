using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces.SaleManagement;
using PharmaCare.Application.Interfaces.AccountManagement;
using PharmaCare.Application.Utilities;
using PharmaCare.Domain.Models.SaleManagement;
using PharmaCare.Domain.Models.AccountManagement;
using PharmaCare.Domain.Models.Configuration;
using PharmaCare.Infrastructure.Interfaces;
using PharmaCare.Infrastructure.Interfaces.Accounting;
using PharmaCare.Infrastructure.Interfaces.Inventory;

namespace PharmaCare.Application.Implementations.SaleManagement;

public class SalesReturnService : ISalesReturnService
{
    private readonly IRepository<SalesReturn> _returnRepo;
    private readonly IRepository<Sale> _saleRepo;
    private readonly IRepository<SaleLine> _saleLineRepo;
    private readonly IRepository<ProductBatch> _batchRepo;
    private readonly IAccountingService _accountingService;
    private readonly IJournalPostingEngine _postingEngine;
    private readonly IInventoryAccountingService _inventoryAccountingService;

    // Account Type IDs
    private const int CASH_ACCOUNT_TYPE = 1;

    public SalesReturnService(
        IRepository<SalesReturn> returnRepo,
        IRepository<Sale> saleRepo,
        IRepository<SaleLine> saleLineRepo,
        IRepository<ProductBatch> batchRepo,
        IAccountingService accountingService,
        IJournalPostingEngine postingEngine,
        IInventoryAccountingService inventoryAccountingService)
    {
        _returnRepo = returnRepo;
        _saleRepo = saleRepo;
        _saleLineRepo = saleLineRepo;
        _batchRepo = batchRepo;
        _accountingService = accountingService;
        _postingEngine = postingEngine;
        _inventoryAccountingService = inventoryAccountingService;
    }

    public async Task<int> CreateReturn(SalesReturn salesReturn, int userId)
    {
        // 1. Validate original sale exists
        var sale = await _saleRepo.FindByCondition(s => s.SaleID == salesReturn.Sale_ID)
            .Include(s => s.SaleLines)
            .FirstOrDefaultAsync();

        if (sale == null)
            throw new InvalidOperationException("Original sale not found");

        if (sale.Status == "Voided")
            throw new InvalidOperationException("Cannot return items from a voided sale");

        // 2. Get previously returned quantities for this sale
        var existingReturns = await GetReturnsBySale(salesReturn.Sale_ID);
        var alreadyReturnedQuantities = new Dictionary<int, decimal>();

        foreach (var existingReturn in existingReturns)
        {
            foreach (var line in existingReturn.ReturnLines)
            {
                if (line.SaleLine_ID.HasValue)
                {
                    if (!alreadyReturnedQuantities.ContainsKey(line.SaleLine_ID.Value))
                        alreadyReturnedQuantities[line.SaleLine_ID.Value] = 0;
                    alreadyReturnedQuantities[line.SaleLine_ID.Value] += line.Quantity;
                }
            }
        }

        // 3. Validate return quantities against remaining returnable quantities
        foreach (var line in salesReturn.ReturnLines)
        {
            if (!line.SaleLine_ID.HasValue) continue;

            var saleLine = sale.SaleLines.FirstOrDefault(sl => sl.SaleLineID == line.SaleLine_ID);
            if (saleLine == null) continue;

            var alreadyReturned = alreadyReturnedQuantities.GetValueOrDefault(line.SaleLine_ID.Value, 0);
            var remainingReturnable = saleLine.Quantity - alreadyReturned;

            if (line.Quantity > remainingReturnable)
            {
                throw new InvalidOperationException(
                    $"Return quantity ({line.Quantity}) exceeds remaining returnable quantity ({remainingReturnable}) for this item. Already returned: {alreadyReturned}");
            }
        }

        // 4. Generate return number and set properties
        salesReturn.ReturnNumber = await GenerateReturnNumber();
        salesReturn.ReturnDate = DateTime.Now;
        salesReturn.Status = "Completed";
        salesReturn.CreatedBy = userId;
        salesReturn.CreatedDate = DateTime.Now;

        // Calculate total
        salesReturn.TotalAmount = salesReturn.ReturnLines.Sum(l => l.Amount);

        // 5. Insert sales return first to get the ID
        await _returnRepo.Insert(salesReturn);

        // 6. Process inventory restock and accounting via InventoryAccountingService
        // Only process lines that have RestockInventory = true
        var restockLines = salesReturn.ReturnLines.Where(l => l.RestockInventory && l.ProductBatch_ID.HasValue).ToList();

        if (restockLines.Any())
        {
            // Get batch cost prices for proper COGS reversal
            var batchIds = restockLines.Select(l => l.ProductBatch_ID!.Value).Distinct().ToList();
            var batches = await _batchRepo.FindByCondition(b => batchIds.Contains(b.ProductBatchID)).ToListAsync();
            var batchCosts = batches.ToDictionary(b => b.ProductBatchID, b => b.CostPrice);

            var returnLineDtos = restockLines.Select(line => new SaleReturnLineDto
            {
                ProductBatchId = line.ProductBatch_ID!.Value,
                Quantity = line.Quantity,
                UnitCost = batchCosts.GetValueOrDefault(line.ProductBatch_ID!.Value, line.UnitPrice),
                UnitPrice = line.UnitPrice
            }).ToList();

            // Get cash account for refund
            var cashAccount = await _accountingService.GetFirstAccountByTypeId(CASH_ACCOUNT_TYPE);
            if (cashAccount == null)
                throw new InvalidOperationException("Cash account not found for refund processing");

            var result = await _inventoryAccountingService.ProcessSaleReturnAsync(
                storeId: salesReturn.Store_ID,
                saleReturnId: salesReturn.SalesReturnID,
                returnDate: salesReturn.ReturnDate,
                returnLines: returnLineDtos,
                paymentAccountId: cashAccount.AccountID,
                userId: userId);

            if (!result.Success)
            {
                throw new InvalidOperationException($"Failed to process inventory restock: {result.ErrorMessage}");
            }

            // Update sales return with journal entry reference
            salesReturn.JournalEntry_ID = result.JournalEntryId;
            await _returnRepo.Update(salesReturn);
        }
        else
        {
            // No inventory restock needed, create simple accounting entry for refund only
            var salesAccount = await _accountingService.GetFirstAccountByTypeId(5); // Sales Account
            var cashAccount = await _accountingService.GetFirstAccountByTypeId(CASH_ACCOUNT_TYPE);

            if (salesAccount == null || cashAccount == null)
                throw new InvalidOperationException("Required accounts not found");

            var journalLines = new List<JournalEntryLine>
            {
                new JournalEntryLine
                {
                    Account_ID = salesAccount.AccountID,
                    DebitAmount = salesReturn.TotalAmount,
                    CreditAmount = 0,
                    Description = $"Sales return - {salesReturn.ReturnNumber}",
                    Store_ID = salesReturn.Store_ID
                },
                new JournalEntryLine
                {
                    Account_ID = cashAccount.AccountID,
                    DebitAmount = 0,
                    CreditAmount = salesReturn.TotalAmount,
                    Description = $"Refund for return - {salesReturn.ReturnNumber}",
                    Store_ID = salesReturn.Store_ID
                }
            };

            var journal = await _postingEngine.CreateAndPostAsync(
                entryType: "SalesReturn",
                description: $"Sales Return - {salesReturn.ReturnNumber}",
                lines: journalLines,
                sourceTable: "SalesReturns",
                sourceId: salesReturn.SalesReturnID,
                storeId: salesReturn.Store_ID,
                userId: userId,
                isSystemEntry: true);

            salesReturn.JournalEntry_ID = journal.JournalEntryID;
            await _returnRepo.Update(salesReturn);
        }

        // 7. Update original sale status if fully returned
        var totalReturnedNow = salesReturn.TotalAmount;
        var previouslyReturned = existingReturns.Sum(r => r.TotalAmount);
        var totalAllReturns = totalReturnedNow + previouslyReturned;

        if (totalAllReturns >= sale.Total)
        {
            sale.Status = "Returned";
            await _saleRepo.Update(sale);
        }

        return salesReturn.SalesReturnID;
    }

    public async Task<SalesReturn?> GetReturnById(int id)
    {
        return await _returnRepo.FindByCondition(r => r.SalesReturnID == id)
            .Include(r => r.Sale)
            .Include(r => r.Party)
            .Include(r => r.Store)
            .Include(r => r.ReturnLines)
                .ThenInclude(l => l.Product)
            .FirstOrDefaultAsync();
    }

    public async Task<List<SalesReturn>> GetReturnsBySale(int saleId)
    {
        return await _returnRepo.FindByCondition(r => r.Sale_ID == saleId && r.Status != "Cancelled")
            .Include(r => r.ReturnLines)
            .OrderByDescending(r => r.ReturnDate)
            .ToListAsync();
    }

    public async Task<List<SalesReturn>> GetReturns(DateTime? startDate = null, DateTime? endDate = null, int? storeId = null)
    {
        var query = _returnRepo.FindByCondition(r => true);

        if (startDate.HasValue)
            query = query.Where(r => r.ReturnDate >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(r => r.ReturnDate <= endDate.Value.AddDays(1));

        if (storeId.HasValue && storeId.Value > 0)
            query = query.Where(r => r.Store_ID == storeId.Value);

        return await query
            .Include(r => r.Sale)
            .Include(r => r.Party)
            .Include(r => r.Store)
            .Include(r => r.ReturnLines)
            .OrderByDescending(r => r.ReturnDate)
            .ToListAsync();
    }

    public async Task<bool> CancelReturn(int returnId, int userId)
    {
        var salesReturn = await _returnRepo.FindByCondition(r => r.SalesReturnID == returnId)
            .Include(r => r.ReturnLines)
            .FirstOrDefaultAsync();

        if (salesReturn == null)
            throw new InvalidOperationException("Return not found");

        if (salesReturn.Status == "Cancelled")
            throw new InvalidOperationException("Return is already cancelled");

        // Void journal entry (this will also create reversal entries)
        if (salesReturn.JournalEntry_ID.HasValue)
        {
            await _accountingService.VoidJournalEntry(salesReturn.JournalEntry_ID.Value, userId);
        }

        // Note: When cancelling a return, the inventory restock would ideally also be reversed
        // via stock movements. However, since we voided the journal entry, the accounting is correct.
        // For full inventory reversal, you would need to call a reverse method on InventoryAccountingService
        // This is a trade-off for simplicity - the voided journal provides an audit trail

        salesReturn.Status = "Cancelled";
        salesReturn.UpdatedBy = userId;
        salesReturn.UpdatedDate = DateTime.Now;

        // Update original sale status back if it was marked as "Returned"
        var sale = await _saleRepo.FindByCondition(s => s.SaleID == salesReturn.Sale_ID).FirstOrDefaultAsync();
        if (sale != null && sale.Status == "Returned")
        {
            // Check if there are any remaining non-cancelled returns
            var remainingReturns = await _returnRepo.FindByCondition(
                r => r.Sale_ID == salesReturn.Sale_ID &&
                     r.SalesReturnID != returnId &&
                     r.Status != "Cancelled")
                .ToListAsync();

            var remainingReturnTotal = remainingReturns.Sum(r => r.TotalAmount);
            if (remainingReturnTotal < sale.Total)
            {
                sale.Status = "Completed";
                await _saleRepo.Update(sale);
            }
        }

        return await _returnRepo.Update(salesReturn);
    }

    public Task<string> GenerateReturnNumber()
    {
        return Task.FromResult(UniqueIdGenerator.Generate("SR"));
    }

    public async Task<Sale?> GetSaleForReturn(int saleId)
    {
        return await _saleRepo.FindByCondition(s => s.SaleID == saleId)
            .Include(s => s.Party)
            .Include(s => s.SaleLines)
                .ThenInclude(l => l.Product)
            .Include(s => s.SaleLines)
                .ThenInclude(l => l.ProductBatch)
            .FirstOrDefaultAsync();
    }
}
