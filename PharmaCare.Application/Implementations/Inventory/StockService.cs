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
using PharmaCare.Domain.Models.AccountManagement;
using PharmaCare.Infrastructure.Interfaces.Accounting;
using PharmaCare.Application.Interfaces.AccountManagement;

namespace PharmaCare.Application.Implementations.Inventory;

public class StockService : IStockService
{
    private readonly IRepository<StoreInventory> _inventoryRepo;
    private readonly IRepository<Product> _productRepo;
    private readonly IRepository<ProductBatch> _batchRepo;
    private readonly IRepository<PurchaseReturn> _returnRepo;
    private readonly IRepository<PurchaseReturnItem> _returnItemRepo;
    private readonly PharmaCareDBContext _context;
    private readonly IVoucherService _voucherService;
    private readonly IStockTransactionService _stockTransactionService;

    public StockService(
        IRepository<StoreInventory> inventoryRepo,
        IRepository<Product> productRepo,
        IRepository<ProductBatch> batchRepo,
        IRepository<PurchaseReturn> returnRepo,
        IRepository<PurchaseReturnItem> returnItemRepo,
        PharmaCareDBContext context,
        IVoucherService voucherService,
        IStockTransactionService stockTransactionService)
    {
        _inventoryRepo = inventoryRepo;
        _productRepo = productRepo;
        _batchRepo = batchRepo;
        _returnRepo = returnRepo;
        _returnItemRepo = returnItemRepo;
        _context = context;
        _voucherService = voucherService;
        _stockTransactionService = stockTransactionService;
    }

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

    #region Purchase Return
    public async Task<List<PurchaseReturn>> GetPurchaseReturns()
    {
        return await _returnRepo.GetAllWithInclude(r => r.Party, r => r.Store, r => r.PurchaseReturnItems)
            .OrderByDescending(r => r.ReturnDate)
            .ToListAsync();
    }
    public async Task<bool> CreatePurchaseReturn(PurchaseReturn purchaseReturn, int loginUserId)
    {
        // Validate sufficient stock for all items before proceeding
        foreach (var item in purchaseReturn.PurchaseReturnItems)
        {
            var inventory = await _inventoryRepo.FindByCondition(i => i.Store_ID == purchaseReturn.Store_ID && i.ProductBatch_ID == item.ProductBatch_ID).FirstOrDefaultAsync();
            if (inventory == null || inventory.QuantityOnHand < item.Quantity)
            {
                return false; // Insufficient stock
            }
        }

        // Create unified transaction via IStockTransactionService (InvoiceType=4 for PURCH_RTN)
        var lines = new List<CreateTransactionLineRequest>();
        foreach (var item in purchaseReturn.PurchaseReturnItems)
        {
            var batch = await _batchRepo.GetByIdAsync(item.ProductBatch_ID);
            lines.Add(new CreateTransactionLineRequest
            {
                ProductId = batch?.Product_ID ?? 0,
                ProductBatchId = item.ProductBatch_ID,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                PurchasePrice = batch?.CostPrice ?? item.UnitPrice,
                ReturnReason = purchaseReturn.Remarks
            });
        }

        var request = new CreateTransactionRequest
        {
            InvoiceTypeId = 4, // PURCH_RTN
            InvoiceDate = DateTime.Now,
            StoreId = purchaseReturn.Store_ID,
            PartyId = purchaseReturn.Party_ID,
            Remarks = purchaseReturn.Remarks,
            CreatedBy = loginUserId,
            Lines = lines
        };

        var transaction = await _stockTransactionService.CreateTransactionAsync(request);
        if (transaction == null) return false;

        // Complete the transaction to update inventory
        var completed = await _stockTransactionService.CompleteTransactionAsync(transaction.StockMainID, loginUserId);
        
        // Update reference StockMain (original purchase) balance if applicable
        if (completed && purchaseReturn.StockMain_ID.HasValue)
        {
            var originalPurchase = await _context.Set<StockMain>()
                .FirstOrDefaultAsync(s => s.StockMainID == purchaseReturn.StockMain_ID.Value);
            if (originalPurchase != null)
            {
                originalPurchase.ReturnedAmount += transaction.TotalAmount;
                originalPurchase.BalanceAmount = originalPurchase.TotalAmount - originalPurchase.PaidAmount - originalPurchase.ReturnedAmount;
                if (originalPurchase.BalanceAmount <= 0)
                {
                    originalPurchase.PaymentStatus = "Paid";
                    originalPurchase.BalanceAmount = 0;
                }
                await _context.SaveChangesAsync();
            }
        }

        return completed;
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

        purchaseReturn.Status = "Approved";
        purchaseReturn.UpdatedBy = loginUserId;
        purchaseReturn.UpdatedDate = DateTime.Now;
        return await _returnRepo.Update(purchaseReturn);
    }
    public async Task<List<ReturnableItemDto>> GetReturnableItems(int partyId, int storeId)
    {
        // Query inventory with StockMain (purchase transactions) for the supplier
        var inventory = await _context.Set<StoreInventory>().AsNoTracking()
            .Where(i => i.Store_ID == storeId && i.QuantityOnHand > 0)
            .Include(i => i.ProductBatch)
                .ThenInclude(pb => pb.StockMain)
            .Include(i => i.ProductBatch)
                .ThenInclude(pb => pb.Product)
            .Where(i => i.ProductBatch.StockMain_ID != null && i.ProductBatch.StockMain.Party_ID == partyId)
            .Select(i => new ReturnableItemDto
            {
                ProductBatchID = i.ProductBatch_ID,
                StockMainID = i.ProductBatch.StockMain_ID,
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
        
        // Voucher Types
        const int BANK_RECEIPT_VOUCHER = 3;
        const int CASH_RECEIPT_VOUCHER = 5;

        // Get account IDs for journal entry
        var paymentAccountTypeId = refundMethod == "Bank" ? BANK_ACCOUNT_TYPE : CASH_ACCOUNT_TYPE;
        var voucherTypeId = refundMethod == "Bank" ? BANK_RECEIPT_VOUCHER : CASH_RECEIPT_VOUCHER;

        var supplierAccount = await _context.Set<ChartOfAccount>()
            .FirstOrDefaultAsync(a => a.AccountType_ID == SUPPLIER_ACCOUNT_TYPE && a.IsActive);
        var paymentAccount = await _context.Set<ChartOfAccount>()
            .FirstOrDefaultAsync(a => a.AccountType_ID == paymentAccountTypeId && a.IsActive);

        if (supplierAccount == null || paymentAccount == null)
        {
            Console.WriteLine("Required accounts not found for refund processing");
            return false;
        }

        // Create Voucher Request
        var voucherRequest = new CreateVoucherRequest
        {
            VoucherTypeId = voucherTypeId,
            VoucherDate = DateTime.Now,
            SourceTable = "PurchaseReturns",
            SourceId = purchaseReturnId,
            StoreId = purchaseReturn.Store_ID,
            Narration = $"Supplier Refund - Purchase Return #{purchaseReturnId} ({refundMethod})",
            CreatedBy = loginUserId,
            Lines = new List<CreateVoucherLineRequest>
            {
                // DR: Cash/Bank (Asset Increase)
                new CreateVoucherLineRequest
                {
                    AccountId = paymentAccount.AccountID,
                    Dr = purchaseReturn.RefundAmount,
                    Cr = 0,
                    Particulars = $"Refund received via {refundMethod} - PR #{purchaseReturnId}",
                    StoreId = purchaseReturn.Store_ID
                },
                // CR: Supplier (Liability Decrease/Refund)
                new CreateVoucherLineRequest
                {
                    AccountId = supplierAccount.AccountID,
                    Dr = 0,
                    Cr = purchaseReturn.RefundAmount,
                    Particulars = $"Supplier refund received - PR #{purchaseReturnId}",
                    StoreId = purchaseReturn.Store_ID
                }
            }
        };

        var voucher = await _voucherService.CreateVoucherAsync(voucherRequest);

        // Update purchase return with refund details
        purchaseReturn.RefundMethod = refundMethod;
        purchaseReturn.RefundStatus = "Received";
        purchaseReturn.RefundVoucher_ID = voucher.VoucherID; // Use new Voucher column
        purchaseReturn.Status = "Completed";
        purchaseReturn.UpdatedBy = loginUserId;
        purchaseReturn.UpdatedDate = DateTime.Now;

        return await _returnRepo.Update(purchaseReturn);
    }

    #endregion

    #region Batch Search
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
