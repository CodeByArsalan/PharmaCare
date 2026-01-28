using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces.Inventory;
using PharmaCare.Application.Interfaces.SaleManagement;
using PharmaCare.Application.Utilities;
using PharmaCare.Domain.Models.Configuration;
using PharmaCare.Domain.Models.Inventory;
using PharmaCare.Infrastructure.Interfaces;

namespace PharmaCare.Application.Implementations.SaleManagement;

/// <summary>
/// Sales Return Service - Refactored to use unified StockMain/StockDetail tables
/// </summary>
public class SalesReturnService : ISalesReturnService
{
    private readonly IStockTransactionService _stockTransactionService;
    private readonly IRepository<StockMain> _stockMainRepo;
    private readonly IRepository<ProductBatch> _batchRepo;

    public SalesReturnService(
        IStockTransactionService stockTransactionService,
        IRepository<StockMain> stockMainRepo,
        IRepository<ProductBatch> batchRepo)
    {
        _stockTransactionService = stockTransactionService;
        _stockMainRepo = stockMainRepo;
        _batchRepo = batchRepo;
    }

    public async Task<int> CreateReturn(CreateSalesReturnRequest request, int userId)
    {
        // 1. Validate original sale (StockMain with InvoiceType=1)
        var sale = await _stockMainRepo.FindByCondition(s => s.StockMainID == request.OriginalSaleId && s.InvoiceType_ID == 1)
            .Include(s => s.StockDetails)
            .FirstOrDefaultAsync();

        if (sale == null)
            throw new InvalidOperationException("Original sale not found");

        if (sale.Status == "Voided")
            throw new InvalidOperationException("Cannot return items from a voided sale");

        // 2. Get previously returned quantities for this sale
        var existingReturns = await GetReturnsBySale(request.OriginalSaleId);
        var alreadyReturnedQuantities = new Dictionary<int, decimal>();

        foreach (var existingReturn in existingReturns)
        {
            foreach (var detail in existingReturn.StockDetails)
            {
                if (detail.ProductBatch_ID.HasValue)
                {
                    if (!alreadyReturnedQuantities.ContainsKey(detail.ProductBatch_ID.Value))
                        alreadyReturnedQuantities[detail.ProductBatch_ID.Value] = 0;
                    alreadyReturnedQuantities[detail.ProductBatch_ID.Value] += detail.Quantity;
                }
            }
        }

        // 3. Validate return quantities
        foreach (var line in request.Lines)
        {
            var saleDetail = sale.StockDetails.FirstOrDefault(d => d.ProductBatch_ID == line.ProductBatchId);
            if (saleDetail == null) continue;

            var alreadyReturned = alreadyReturnedQuantities.GetValueOrDefault(line.ProductBatchId, 0);
            var remainingReturnable = saleDetail.Quantity - alreadyReturned;

            if (line.Quantity > remainingReturnable)
            {
                throw new InvalidOperationException(
                    $"Return quantity ({line.Quantity}) exceeds remaining returnable quantity ({remainingReturnable})");
            }
        }

        // 4. Get batch costs for proper accounting
        var batchIds = request.Lines.Select(l => l.ProductBatchId).Distinct().ToList();
        var batches = await _batchRepo.FindByCondition(b => batchIds.Contains(b.ProductBatchID)).ToListAsync();
        var batchCosts = batches.ToDictionary(b => b.ProductBatchID, b => b.CostPrice);

        // 5. Create return transaction via unified service (InvoiceType=3 for SALE_RTN)
        var transactionRequest = new CreateTransactionRequest
        {
            InvoiceTypeId = 3, // SALE_RTN
            StoreId = request.StoreId,
            PartyId = sale.Party_ID,
            InvoiceDate = DateTime.Now,
            ReferenceStockMainId = request.OriginalSaleId,
            Remarks = request.ReturnReason,
            CreatedBy = userId,
            Lines = request.Lines.Select(l => new CreateTransactionLineRequest
            {
                ProductId = batches.FirstOrDefault(b => b.ProductBatchID == l.ProductBatchId)?.Product_ID ?? 0,
                ProductBatchId = l.ProductBatchId,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                PurchasePrice = batchCosts.GetValueOrDefault(l.ProductBatchId, l.UnitPrice),
                ReturnReason = l.Reason
            }).ToList()
        };

        var transaction = await _stockTransactionService.CreateTransactionAsync(transactionRequest);

        // 6. Update status and generate accounting entries
        await _stockTransactionService.UpdateStatusAsync(transaction.StockMainID, "Completed");
        await _stockTransactionService.GenerateAccountingEntriesAsync(transaction.StockMainID);

        // 7. Check if original sale is fully returned
        var totalReturned = existingReturns.Sum(r => r.TotalAmount) + transaction.TotalAmount;
        if (totalReturned >= sale.TotalAmount)
        {
            sale.Status = "Returned";
            await _stockMainRepo.Update(sale);
        }

        return transaction.StockMainID;
    }

    public async Task<StockMain?> GetReturnById(int id)
    {
        return await _stockTransactionService.GetTransactionAsync(id);
    }

    public async Task<List<StockMain>> GetReturnsBySale(int saleId)
    {
        return await _stockMainRepo.FindByCondition(s => 
            s.InvoiceType_ID == 3 && s.ReferenceStockMain_ID == saleId && s.Status != "Cancelled")
            .Include(s => s.StockDetails)
            .OrderByDescending(s => s.InvoiceDate)
            .ToListAsync();
    }

    public async Task<List<StockMain>> GetReturns(DateTime? startDate = null, DateTime? endDate = null, int? storeId = null)
    {
        var transactions = await _stockTransactionService.GetTransactionsByTypeAsync(3, startDate, endDate);

        if (storeId.HasValue && storeId.Value > 0)
            transactions = transactions.Where(t => t.Store_ID == storeId.Value);

        return transactions.ToList();
    }

    public async Task<bool> CancelReturn(int returnId, int userId)
    {
        var transaction = await _stockTransactionService.GetTransactionAsync(returnId);

        if (transaction == null)
            throw new InvalidOperationException("Return not found");

        if (transaction.Status == "Cancelled")
            throw new InvalidOperationException("Return is already cancelled");

        await _stockTransactionService.VoidTransactionAsync(returnId, "Return cancelled", userId);

        // Restore original sale status if needed
        if (transaction.ReferenceStockMain_ID.HasValue)
        {
            var sale = await _stockMainRepo.GetByIdAsync(transaction.ReferenceStockMain_ID.Value);
            if (sale != null && sale.Status == "Returned")
            {
                var remainingReturns = await _stockMainRepo.FindByCondition(
                    s => s.InvoiceType_ID == 3 && 
                         s.ReferenceStockMain_ID == transaction.ReferenceStockMain_ID &&
                         s.StockMainID != returnId &&
                         s.Status != "Voided" && s.Status != "Cancelled")
                    .ToListAsync();

                if (remainingReturns.Sum(r => r.TotalAmount) < sale.TotalAmount)
                {
                    sale.Status = "Completed";
                    await _stockMainRepo.Update(sale);
                }
            }
        }

        return true;
    }

    public async Task<StockMain?> GetSaleForReturn(int saleId)
    {
        return await _stockMainRepo.FindByCondition(s => s.StockMainID == saleId && s.InvoiceType_ID == 1)
            .Include(s => s.Party)
            .Include(s => s.StockDetails)
                .ThenInclude(d => d.Product)
            .Include(s => s.StockDetails)
                .ThenInclude(d => d.ProductBatch)
            .FirstOrDefaultAsync();
    }
}

/// <summary>
/// Request DTO for creating a sales return
/// </summary>
public class CreateSalesReturnRequest
{
    public int OriginalSaleId { get; set; }
    public int StoreId { get; set; }
    public string? ReturnReason { get; set; }
    public string RefundMethod { get; set; } = "Cash";
    public List<CreateSalesReturnLineRequest> Lines { get; set; } = new();
}

public class CreateSalesReturnLineRequest
{
    public int ProductBatchId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string? Reason { get; set; }
    public bool RestockInventory { get; set; } = true;
}
