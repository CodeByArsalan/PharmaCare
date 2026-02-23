using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.Application.Implementations.Transactions;

/// <summary>
/// Service for managing Purchase Orders.
/// </summary>
public class PurchaseOrderService : IPurchaseOrderService
{
    private readonly IRepository<StockMain> _stockMainRepository;
    private readonly IRepository<TransactionType> _transactionTypeRepository;
    private readonly IUnitOfWork _unitOfWork;

    private const string TRANSACTION_TYPE_CODE = "PO";
    private const string PREFIX = "PO";

    public PurchaseOrderService(
        IRepository<StockMain> stockMainRepository,
        IRepository<TransactionType> transactionTypeRepository,
        IUnitOfWork unitOfWork)
    {
        _stockMainRepository = stockMainRepository;
        _transactionTypeRepository = transactionTypeRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<StockMain>> GetAllAsync()
    {
        return await _stockMainRepository.Query()
            .Include(s => s.TransactionType)
            .Include(s => s.Party)
            .Where(s => s.TransactionType!.Code == TRANSACTION_TYPE_CODE)
            .OrderByDescending(s => s.TransactionDate)
            .ThenByDescending(s => s.StockMainID)
            .ToListAsync();
    }

    public async Task<StockMain?> GetByIdAsync(int id)
    {
        return await _stockMainRepository.Query()
            .Include(s => s.TransactionType)
            .Include(s => s.Party)
            .Include(s => s.StockDetails)
                .ThenInclude(d => d.Product)
            .FirstOrDefaultAsync(s => s.StockMainID == id && s.TransactionType!.Code == TRANSACTION_TYPE_CODE);
    }

    public async Task<StockMain> CreateAsync(StockMain purchaseOrder, int userId)
    {
        // Get the PO transaction type
        var transactionType = await _transactionTypeRepository.Query()
            .FirstOrDefaultAsync(t => t.Code == TRANSACTION_TYPE_CODE);

        if (transactionType == null)
            throw new InvalidOperationException($"Transaction type '{TRANSACTION_TYPE_CODE}' not found.");

        purchaseOrder.TransactionType_ID = transactionType.TransactionTypeID;
        purchaseOrder.TransactionNo = await GenerateTransactionNoAsync();
        purchaseOrder.Status = "Draft";
        purchaseOrder.PaymentStatus = "Unpaid";
        purchaseOrder.CreatedAt = DateTime.Now;
        purchaseOrder.CreatedBy = userId;

        NormalizePurchaseOrderLines(purchaseOrder);

        // Calculate totals
        CalculateTotals(purchaseOrder);

        await _stockMainRepository.AddAsync(purchaseOrder);
        await _unitOfWork.SaveChangesAsync();

        return purchaseOrder;
    }

    public async Task<StockMain> UpdateAsync(StockMain purchaseOrder, int userId)
    {
        var existing = await GetByIdAsync(purchaseOrder.StockMainID);
        if (existing == null)
            throw new InvalidOperationException("Purchase Order not found.");

        if (existing.Status != "Draft")
            throw new InvalidOperationException("Only draft purchase orders can be edited.");

        // Update fields
        existing.Party_ID = purchaseOrder.Party_ID;
        existing.TransactionDate = purchaseOrder.TransactionDate;
        existing.DiscountPercent = purchaseOrder.DiscountPercent;
        existing.DiscountAmount = purchaseOrder.DiscountAmount;
        existing.Remarks = purchaseOrder.Remarks;
        existing.UpdatedAt = DateTime.Now;
        existing.UpdatedBy = userId;

        NormalizePurchaseOrderLines(purchaseOrder);

        // Clear and re-add details
        existing.StockDetails.Clear();
        foreach (var detail in purchaseOrder.StockDetails)
        {
            existing.StockDetails.Add(new StockDetail
            {
                Product_ID = detail.Product_ID,
                Quantity = detail.Quantity,
                UnitPrice = detail.UnitPrice,
                CostPrice = detail.CostPrice,
                DiscountPercent = detail.DiscountPercent,
                DiscountAmount = detail.DiscountAmount,
                LineTotal = detail.LineTotal,
                LineCost = detail.LineCost,
                Remarks = detail.Remarks
            });
        }

        // Recalculate totals
        CalculateTotals(existing);

        _stockMainRepository.Update(existing);
        await _unitOfWork.SaveChangesAsync();

        return existing;
    }

    public async Task<bool> ApproveAsync(int id, int userId)
    {
        var purchaseOrder = await GetByIdAsync(id);
        if (purchaseOrder == null)
            return false;

        if (purchaseOrder.Status != "Draft")
            return false;

        purchaseOrder.Status = "Approved";
        purchaseOrder.UpdatedAt = DateTime.Now;
        purchaseOrder.UpdatedBy = userId;

        _stockMainRepository.Update(purchaseOrder);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }

    public async Task<bool> ToggleStatusAsync(int id, int userId)
    {
        var purchaseOrder = await GetByIdAsync(id);
        if (purchaseOrder == null)
            return false;

        // Toggle by voiding (can't truly delete)
        if (purchaseOrder.Status == "Void")
            return false;

        purchaseOrder.Status = "Void";
        purchaseOrder.VoidedAt = DateTime.Now;
        purchaseOrder.VoidedBy = userId;
        purchaseOrder.VoidReason = "Cancelled by user";

        _stockMainRepository.Update(purchaseOrder);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }

    public async Task<IEnumerable<StockMain>> GetApprovedPurchaseOrdersAsync(int? supplierId = null)
    {
        var query = _stockMainRepository.Query()
            .Include(s => s.TransactionType)
            .Include(s => s.Party)
            .Include(s => s.StockDetails)
                .ThenInclude(d => d.Product)
            .Where(s => s.TransactionType!.Code == TRANSACTION_TYPE_CODE && s.Status == "Approved");

        if (supplierId.HasValue)
        {
            query = query.Where(s => s.Party_ID == supplierId.Value);
        }

        return await query
            .OrderByDescending(s => s.TransactionDate)
            .ToListAsync();
    }

    private async Task<string> GenerateTransactionNoAsync()
    {
        var datePrefix = $"{PREFIX}-{DateTime.Now:yyyyMMdd}-";

        var lastTransaction = await _stockMainRepository.Query()
            .Where(s => s.TransactionNo.StartsWith(datePrefix))
            .OrderByDescending(s => s.TransactionNo)
            .FirstOrDefaultAsync();

        int nextNum = 1;
        if (lastTransaction != null)
        {
            var parts = lastTransaction.TransactionNo.Split('-');
            if (parts.Length > 2 && int.TryParse(parts.Last(), out int lastNum))
            {
                nextNum = lastNum + 1;
            }
        }

        return $"{datePrefix}{nextNum:D4}";
    }

    private void CalculateTotals(StockMain purchaseOrder)
    {
        purchaseOrder.SubTotal = purchaseOrder.StockDetails.Sum(d => d.LineTotal);
        
        if (purchaseOrder.DiscountPercent > 0)
        {
            purchaseOrder.DiscountAmount = Math.Round(purchaseOrder.SubTotal * purchaseOrder.DiscountPercent / 100, 2);
        }

        purchaseOrder.TotalAmount = purchaseOrder.SubTotal - purchaseOrder.DiscountAmount;
        if (purchaseOrder.PaidAmount > purchaseOrder.TotalAmount)
        {
            throw new InvalidOperationException("Paid amount cannot exceed total amount.");
        }

        purchaseOrder.BalanceAmount = purchaseOrder.TotalAmount - purchaseOrder.PaidAmount;
    }

    private static void NormalizePurchaseOrderLines(StockMain purchaseOrder)
    {
        if (purchaseOrder.StockDetails == null || purchaseOrder.StockDetails.Count == 0)
        {
            throw new InvalidOperationException("At least one item is required.");
        }

        foreach (var detail in purchaseOrder.StockDetails)
        {
            if (detail.Quantity <= 0)
            {
                throw new InvalidOperationException("Each line item must have a quantity greater than zero.");
            }

            var unitRate = detail.UnitPrice > 0 ? detail.UnitPrice : detail.CostPrice;
            if (unitRate < 0)
            {
                throw new InvalidOperationException("Unit price cannot be negative.");
            }

            var grossAmount = Math.Round(detail.Quantity * unitRate, 2);
            var lineDiscount = detail.DiscountPercent > 0
                ? Math.Round(grossAmount * detail.DiscountPercent / 100, 2)
                : Math.Round(Math.Max(0, detail.DiscountAmount), 2);

            if (lineDiscount > grossAmount)
            {
                throw new InvalidOperationException("Line discount cannot exceed line amount.");
            }

            detail.UnitPrice = unitRate;
            detail.CostPrice = detail.CostPrice > 0 ? detail.CostPrice : unitRate;
            detail.DiscountAmount = lineDiscount;
            detail.LineTotal = Math.Round(grossAmount - lineDiscount, 2);
            detail.LineCost = Math.Round(detail.Quantity * detail.CostPrice, 2);
        }
    }
}
