using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces.Inventory;
using PharmaCare.Application.Interfaces.PurchaseManagement;
using PharmaCare.Application.DTOs.Inventory;
using PharmaCare.Application.Utilities;
using PharmaCare.Domain.Models.Inventory;
using PharmaCare.Domain.Models.Configuration;
using PharmaCare.Infrastructure.Interfaces;

namespace PharmaCare.Application.Implementations.PurchaseManagement;

/// <summary>
/// Purchase Service - Refactored to use unified StockMain/StockDetail tables
/// </summary>
public class PurchaseService : IPurchaseService
{
    private readonly IStockTransactionService _stockTransactionService;
    private readonly IRepository<StockMain> _stockMainRepo;
    private readonly IRepository<ProductBatch> _batchRepo;
    private readonly IRepository<Party> _partyRepo;
    private readonly IPurchaseOrderService _poService;

    public PurchaseService(
        IStockTransactionService stockTransactionService,
        IRepository<StockMain> stockMainRepo,
        IRepository<ProductBatch> batchRepo,
        IRepository<Party> partyRepo,
        IPurchaseOrderService poService)
    {
        _stockTransactionService = stockTransactionService;
        _stockMainRepo = stockMainRepo;
        _batchRepo = batchRepo;
        _partyRepo = partyRepo;
        _poService = poService;
    }

    public async Task<List<StockMain>> GetPurchases()
    {
        // InvoiceType_ID = 2 is PURCHASE
        var transactions = await _stockTransactionService.GetTransactionsByTypeAsync(2);
        return transactions.ToList();
    }

    public async Task<StockMain?> GetPurchaseById(int id)
    {
        return await _stockTransactionService.GetTransactionAsync(id);
    }

    public async Task<bool> CreatePurchase(CreatePurchaseRequest request, int loginUserId)
    {
        // 1. Validation: Check Batch Uniqueness
        var batchNumbers = request.Items.Select(i => i.BatchNumber).ToList();
        
        if (batchNumbers.Count != batchNumbers.Distinct().Count())
            return false; // Internal duplication

        foreach (var batchNum in batchNumbers)
        {
            var exists = (await _batchRepo.GetAll()).Any(b => b.BatchNumber == batchNum);
            if (exists) return false;
        }

        // 2. Create ProductBatches for all items first
        var createdBatches = new Dictionary<string, ProductBatch>();
        foreach (var item in request.Items)
        {
            var existingBatch = _batchRepo.FindByCondition(b => 
                b.Product_ID == item.ProductId && b.BatchNumber == item.BatchNumber).FirstOrDefault();
            
            if (existingBatch == null)
            {
                var newBatch = new ProductBatch
                {
                    Product_ID = item.ProductId,
                    BatchNumber = item.BatchNumber,
                    ExpiryDate = item.ExpiryDate,
                    CostPrice = item.CostPrice,
                    SellingPrice = item.SellingPrice,
                    MRP = item.SellingPrice,
                    CreatedBy = loginUserId,
                    CreatedDate = DateTime.Now
                };
                newBatch = await _batchRepo.InsertAndReturn(newBatch);
                createdBatches[item.BatchNumber] = newBatch;
            }
            else
            {
                createdBatches[item.BatchNumber] = existingBatch;
            }
        }

        // 3. Create Purchase transaction via unified service (InvoiceType=2 for PURCHASE)
        var transactionRequest = new CreateTransactionRequest
        {
            InvoiceTypeId = 2, // PURCHASE
            StoreId = request.StoreId,
            PartyId = request.PartyId,
            InvoiceDate = DateTime.Now,
            SupplierInvoiceNo = request.SupplierInvoiceNo,
            Remarks = request.Remarks,
            CreatedBy = loginUserId,
            Lines = request.Items.Select(i => new CreateTransactionLineRequest
            {
                ProductId = i.ProductId,
                ProductBatchId = createdBatches[i.BatchNumber].ProductBatchID,
                Quantity = i.Quantity,
                UnitPrice = i.SellingPrice,
                PurchasePrice = i.CostPrice
            }).ToList()
        };

        var transaction = await _stockTransactionService.CreateTransactionAsync(transactionRequest);

        // Update batches with StockMain (Purchase) reference
        foreach (var batch in createdBatches.Values)
        {
            batch.StockMain_ID = transaction.StockMainID;
            await _batchRepo.Update(batch);
        }

        // 4. Update status to Completed
        await _stockTransactionService.UpdateStatusAsync(transaction.StockMainID, "Completed");
        await _stockTransactionService.GenerateAccountingEntriesAsync(transaction.StockMainID);

        // 5. Update PO if linked
        if (request.PurchaseOrderId.HasValue)
        {
            var productQuantities = new Dictionary<int, decimal>();
            foreach (var item in request.Items)
            {
                if (productQuantities.ContainsKey(item.ProductId))
                    productQuantities[item.ProductId] += item.Quantity;
                else
                    productQuantities[item.ProductId] = item.Quantity;
            }
            await _poService.UpdateReceivedQuantities(request.PurchaseOrderId.Value, productQuantities);
        }

        return true;
    }

    public async Task<PurchaseSummaryDto> GetPurchaseSummary()
    {
        var today = DateTime.Today;
        var firstDayOfMonth = new DateTime(today.Year, today.Month, 1);

        var allPurchases = await _stockMainRepo.FindByCondition(s => s.InvoiceType_ID == 2)
            .Include(s => s.StockDetails)
            .ToListAsync();

        var todayPurchases = allPurchases.Count(g => g.CreatedDate.Date == today);
        var monthPurchases = allPurchases.Where(g => g.CreatedDate >= firstDayOfMonth).ToList();

        return new PurchaseSummaryDto
        {
            TotalPurchasesToday = todayPurchases,
            TotalPurchasesThisMonth = monthPurchases.Count,
            TotalValueThisMonth = monthPurchases.Sum(g => g.TotalAmount),
            PendingPOs = await _poService.GetPendingPurchaseOrdersCount()
        };
    }
}

/// <summary>
/// Request DTO for creating a Purchase
/// </summary>
public class CreatePurchaseRequest
{
    public int StoreId { get; set; }
    public int? PartyId { get; set; }
    public int? PurchaseOrderId { get; set; }
    public string? SupplierInvoiceNo { get; set; }
    public string? Remarks { get; set; }
    public List<CreatePurchaseItemRequest> Items { get; set; } = new();
}

public class CreatePurchaseItemRequest
{
    public int ProductId { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
    public decimal Quantity { get; set; }
    public decimal CostPrice { get; set; }
    public decimal SellingPrice { get; set; }
}
