using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces.AccountManagement;
using PharmaCare.Application.Interfaces.Inventory;
using PharmaCare.Application.Utilities;
using PharmaCare.Domain.Models.Configuration;
using PharmaCare.Domain.Models.Inventory;
using PharmaCare.Domain.Models.Products;
using PharmaCare.Infrastructure.Interfaces;
using PharmaCare.Infrastructure;

namespace PharmaCare.Application.Implementations.Inventory;

/// <summary>
/// Unified service for all stock transactions using StockMain/StockDetail
/// </summary>
public class StockTransactionService : IStockTransactionService
{
    private readonly IRepository<StockMain> _stockMainRepo;
    private readonly IRepository<StockDetail> _detailRepo;
    private readonly IRepository<InvoiceType> _invoiceTypeRepo;
    private readonly IRepository<Product> _productRepo;
    private readonly IRepository<ProductBatch> _batchRepo;
    private readonly IVoucherService _voucherService;
    private readonly PharmaCareDBContext _context;

    public StockTransactionService(
        IRepository<StockMain> stockMainRepo,
        IRepository<StockDetail> detailRepo,
        IRepository<InvoiceType> invoiceTypeRepo,
        IRepository<Product> productRepo,
        IRepository<ProductBatch> batchRepo,
        IVoucherService voucherService,
        PharmaCareDBContext context)
    {
        _stockMainRepo = stockMainRepo;
        _detailRepo = detailRepo;
        _invoiceTypeRepo = invoiceTypeRepo;
        _productRepo = productRepo;
        _batchRepo = batchRepo;
        _voucherService = voucherService;
        _context = context;
    }

    public async Task<StockMain> CreateTransactionAsync(CreateTransactionRequest request)
    {
        var invoiceType = await _invoiceTypeRepo.GetByIdAsync(request.InvoiceTypeId)
            ?? throw new InvalidOperationException($"InvoiceType {request.InvoiceTypeId} not found");

        var invoiceNo = GenerateInvoiceNumber(invoiceType.Code);

        var transaction = new StockMain
        {
            InvoiceType_ID = request.InvoiceTypeId,
            InvoiceNo = invoiceNo,
            InvoiceDate = request.InvoiceDate,
            Store_ID = request.StoreId,
            Party_ID = request.PartyId,
            DiscountPercent = request.DiscountPercent,
            DiscountAmount = request.DiscountAmount,
            PaidAmount = request.PaidAmount,
            Account_ID = request.AccountId,
            Remarks = request.Remarks,
            SupplierInvoiceNo = request.SupplierInvoiceNo,
            ReferenceStockMain_ID = request.ReferenceStockMainId,
            DestinationStore_ID = request.DestinationStoreId,
            Status = "Draft",
            CreatedBy = request.CreatedBy,
            CreatedDate = DateTime.Now
        };

        foreach (var line in request.Lines)
        {
            // Get purchase price from batch if available
            decimal purchasePrice = line.PurchasePrice;
            if (purchasePrice <= 0 && line.ProductBatchId.HasValue)
            {
                var batch = await _batchRepo.GetByIdAsync(line.ProductBatchId.Value);
                purchasePrice = batch?.CostPrice ?? 0;
            }

            var detail = new StockDetail
            {
                Product_ID = line.ProductId,
                ProductBatch_ID = line.ProductBatchId,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                PurchasePrice = purchasePrice,
                DiscountPercent = line.DiscountPercent,
                DiscountAmount = line.DiscountAmount,
                SystemQuantity = line.SystemQuantity,
                PhysicalQuantity = line.PhysicalQuantity,
                ReturnReason = line.ReturnReason,
                MovementType = invoiceType.Code
            };

            var grossAmount = detail.Quantity * detail.UnitPrice;
            detail.LineTotal = grossAmount - detail.DiscountAmount;
            detail.LineCost = detail.Quantity * detail.PurchasePrice;
            detail.TotalCost = detail.LineCost;

            if (invoiceType.Code == "TAKE" && detail.SystemQuantity.HasValue && detail.PhysicalQuantity.HasValue)
            {
                var variance = detail.PhysicalQuantity.Value - detail.SystemQuantity.Value;
                detail.VarianceCost = variance * detail.PurchasePrice;
            }

            transaction.StockDetails.Add(detail);
        }

        transaction.SubTotal = transaction.StockDetails.Sum(d => d.Quantity * d.UnitPrice);
        transaction.TotalAmount = transaction.SubTotal - transaction.DiscountAmount;
        transaction.BalanceAmount = transaction.TotalAmount - transaction.PaidAmount;

        if (transaction.TotalAmount > 0)
        {
            if (transaction.BalanceAmount <= 0)
                transaction.PaymentStatus = "Paid";
            else if (transaction.PaidAmount > 0)
                transaction.PaymentStatus = "Partial";
            else
                transaction.PaymentStatus = "Unpaid";
        }

        return await _stockMainRepo.InsertAndReturn(transaction);
    }

    public async Task AddTransactionLinesAsync(int stockMainId, IEnumerable<CreateTransactionLineRequest> lines)
    {
        var transaction = await _stockMainRepo.FindByCondition(s => s.StockMainID == stockMainId)
            .Include(s => s.InvoiceType)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException($"Transaction {stockMainId} not found");

        foreach (var line in lines)
        {
            decimal purchasePrice = line.PurchasePrice;
            if (purchasePrice <= 0 && line.ProductBatchId.HasValue)
            {
                var batch = await _batchRepo.GetByIdAsync(line.ProductBatchId.Value);
                purchasePrice = batch?.CostPrice ?? 0;
            }

            var detail = new StockDetail
            {
                StockMain_ID = stockMainId,
                Product_ID = line.ProductId,
                ProductBatch_ID = line.ProductBatchId,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                PurchasePrice = purchasePrice,
                DiscountPercent = line.DiscountPercent,
                DiscountAmount = line.DiscountAmount,
                MovementType = transaction.InvoiceType?.Code
            };

            var grossAmount = detail.Quantity * detail.UnitPrice;
            detail.LineTotal = grossAmount - detail.DiscountAmount;
            detail.LineCost = detail.Quantity * detail.PurchasePrice;
            detail.TotalCost = detail.LineCost;

            await _detailRepo.Insert(detail);
        }
    }

    public async Task<StockMain?> GetTransactionAsync(int stockMainId)
    {
        return await _stockMainRepo.FindByCondition(s => s.StockMainID == stockMainId)
            .Include(s => s.InvoiceType)
            .Include(s => s.Store)
            .Include(s => s.Party)
            .Include(s => s.StockDetails)
                .ThenInclude(d => d.Product)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<StockMain>> GetTransactionsByTypeAsync(int invoiceTypeId, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _stockMainRepo.FindByCondition(s => s.InvoiceType_ID == invoiceTypeId)
            .Include(s => s.InvoiceType)
            .Include(s => s.Party)
            .AsQueryable();

        if (fromDate.HasValue)
            query = query.Where(s => s.InvoiceDate >= fromDate.Value);
        if (toDate.HasValue)
            query = query.Where(s => s.InvoiceDate <= toDate.Value);

        return await query.OrderByDescending(s => s.InvoiceDate).ToListAsync();
    }

    public async Task<IEnumerable<StockMain>> GetTransactionsByStoreAsync(int storeId, int? invoiceTypeId = null)
    {
        var query = _stockMainRepo.FindByCondition(s => s.Store_ID == storeId)
            .Include(s => s.InvoiceType)
            .AsQueryable();

        if (invoiceTypeId.HasValue)
            query = query.Where(s => s.InvoiceType_ID == invoiceTypeId.Value);

        return await query.OrderByDescending(s => s.InvoiceDate).ToListAsync();
    }

    public async Task UpdateStatusAsync(int stockMainId, string status)
    {
        var transaction = await _stockMainRepo.GetByIdAsync(stockMainId)
            ?? throw new InvalidOperationException($"Transaction {stockMainId} not found");

        transaction.Status = status;
        if (status == "Completed")
            transaction.CompletedDate = DateTime.Now;

        await _stockMainRepo.Update(transaction);
    }

    public async Task ProcessPaymentAsync(int stockMainId, decimal amount, int accountId, string paymentMethod)
    {
        var transaction = await _stockMainRepo.FindByCondition(s => s.StockMainID == stockMainId)
            .Include(s => s.InvoiceType)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException($"Transaction {stockMainId} not found");

        transaction.PaidAmount += amount;
        transaction.BalanceAmount = transaction.TotalAmount - transaction.PaidAmount;
        transaction.Account_ID = accountId;

        if (transaction.BalanceAmount <= 0)
            transaction.PaymentStatus = "Paid";
        else
            transaction.PaymentStatus = "Partial";

        var voucherRequest = new CreateVoucherRequest
        {
            VoucherTypeId = paymentMethod.ToLower().Contains("cash") ? 5 : 3,
            VoucherDate = DateTime.Now.Date,
            SourceTable = "StockMain",
            SourceId = stockMainId,
            StoreId = transaction.Store_ID,
            Narration = $"Payment for {transaction.InvoiceNo}",
            CreatedBy = transaction.CreatedBy,
            Lines = new List<CreateVoucherLineRequest>
            {
                new() { AccountId = accountId, Dr = amount, Cr = 0, Particulars = "Payment received" }
            }
        };

        var voucher = await _voucherService.CreateVoucherAsync(voucherRequest);
        transaction.PaymentVoucher_ID = voucher.VoucherID;

        await _stockMainRepo.Update(transaction);
    }

    public async Task VoidTransactionAsync(int stockMainId, string reason, int userId)
    {
        var transaction = await _stockMainRepo.FindByCondition(s => s.StockMainID == stockMainId)
            .Include(s => s.PaymentVoucher)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException($"Transaction {stockMainId} not found");

        transaction.Status = "Voided";
        transaction.VoidReason = reason;
        transaction.VoidedBy = userId;
        transaction.VoidedDate = DateTime.Now;

        if (transaction.PaymentVoucher_ID.HasValue && !transaction.PaymentVoucher!.IsReversed)
        {
            await _voucherService.ReverseVoucherAsync(
                transaction.PaymentVoucher_ID.Value,
                $"Void transaction {transaction.InvoiceNo}: {reason}",
                userId);
        }

        await _stockMainRepo.Update(transaction);
    }

    public async Task<int?> GenerateAccountingEntriesAsync(int stockMainId)
    {
        var transaction = await _stockMainRepo.FindByCondition(s => s.StockMainID == stockMainId)
            .Include(s => s.InvoiceType)
            .Include(s => s.StockDetails)
            .FirstOrDefaultAsync();

        if (transaction == null || !transaction.InvoiceType!.CreatesVoucher)
            return null;

        var lines = new List<CreateVoucherLineRequest>();

        // Simple accounting entries - can be enhanced later with category account mappings
        if (transaction.InvoiceType.Code == "SALE" || transaction.InvoiceType.Code == "PURCHASE")
        {
            // For now, just create a single JV line with total
            // Full category-based COGS can be added later
            lines.Add(new() { Dr = transaction.TotalAmount, Cr = 0, AccountId = 1, Particulars = $"{transaction.InvoiceType.Name}" });
        }

        if (!lines.Any())
            return null;

        var voucher = await _voucherService.CreateVoucherAsync(new CreateVoucherRequest
        {
            VoucherTypeId = 1,
            VoucherDate = transaction.InvoiceDate,
            SourceTable = "StockMain",
            SourceId = stockMainId,
            StoreId = transaction.Store_ID,
            Narration = $"Auto-generated from {transaction.InvoiceType.Code} {transaction.InvoiceNo}",
            CreatedBy = transaction.CreatedBy,
            Lines = lines
        });

        transaction.PaymentVoucher_ID = voucher.VoucherID;
        await _stockMainRepo.Update(transaction);

        return voucher.VoucherID;
    }

    public async Task<bool> CompleteTransactionAsync(int stockMainId, int userId)
    {
        var transaction = await _stockMainRepo.FindByCondition(s => s.StockMainID == stockMainId)
            .Include(s => s.InvoiceType)
            .Include(s => s.StockDetails)
            .FirstOrDefaultAsync();

        if (transaction == null || transaction.InvoiceType == null)
            return false;

        // Update inventory based on transaction type
        foreach (var detail in transaction.StockDetails.Where(d => d.ProductBatch_ID.HasValue))
        {
            var storeInventory = await _context.Set<StoreInventory>()
                .FirstOrDefaultAsync(i => i.Store_ID == transaction.Store_ID && i.ProductBatch_ID == detail.ProductBatch_ID);

            if (storeInventory == null && ShouldIncreaseInventory(transaction.InvoiceType.Code))
            {
                // Create inventory record if it doesn't exist (for purchases, transfers in)
                storeInventory = new StoreInventory
                {
                    Store_ID = transaction.Store_ID,
                    ProductBatch_ID = detail.ProductBatch_ID.Value,
                    QuantityOnHand = 0
                };
                _context.Set<StoreInventory>().Add(storeInventory);
            }

            if (storeInventory != null)
            {
                var qtyChange = GetInventoryChange(transaction.InvoiceType.Code, detail.Quantity);
                storeInventory.QuantityOnHand += qtyChange;
            }
        }

        // Update transaction status
        transaction.Status = "Completed";
        transaction.UpdatedBy = userId;
        transaction.UpdatedDate = DateTime.Now;

        await _context.SaveChangesAsync();
        return true;
    }

    private static bool ShouldIncreaseInventory(string typeCode)
    {
        return typeCode == "PURCHASE" || typeCode == "XFER_IN" || typeCode == "SALE_RTN";
    }

    private static decimal GetInventoryChange(string typeCode, decimal quantity)
    {
        return typeCode switch
        {
            "SALE" => -quantity,          // Sales decrease inventory
            "PURCHASE" => quantity,       // Purchases increase inventory
            "SALE_RTN" => quantity,       // Sales returns increase inventory
            "PURCH_RTN" => -quantity,     // Purchase returns decrease inventory
            "ADJ" => quantity,            // Adjustments can be +/-
            "XFER_OUT" => -quantity,      // Transfer out decreases source inventory
            "XFER_IN" => quantity,        // Transfer in increases destination inventory
            "TAKE" => quantity,           // Stock take adjustments (variance)
            _ => 0
        };
    }

    private string GenerateInvoiceNumber(string typeCode)
    {
        var prefix = typeCode switch
        {
            "SALE" => "INV",
            "PURCHASE" => "GRN",
            "SALE_RTN" => "SRN",
            "PURCH_RTN" => "PRN",
            "ADJ" => "ADJ",
            "XFER_OUT" => "XFO",
            "XFER_IN" => "XFI",
            "TAKE" => "STK",
            "PO" => "PO",
            _ => "TXN"
        };
        return UniqueIdGenerator.Generate(prefix);
    }
}
