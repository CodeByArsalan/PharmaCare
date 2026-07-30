using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.DTOs;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Application.Utilities;
using PharmaCare.Domain.Entities.Finance;
using PharmaCare.Domain.Entities.Transactions;
using PharmaCare.Domain.Enums;

namespace PharmaCare.Application.Implementations.Transactions;

/// <summary>
/// Service for managing Purchase Orders.
/// </summary>
public class PurchaseOrderService : IPurchaseOrderService
{
    private readonly IRepository<StockMain> _stockMainRepository;
    private readonly IRepository<TransactionType> _transactionTypeRepository;
    private readonly IRepository<Payment> _paymentRepository;
    private readonly IUnitOfWork _unitOfWork;

    private const string TRANSACTION_TYPE_CODE = "PO";
    private const string GRN_TRANSACTION_TYPE_CODE = "GRN";
    private const string PREFIX = "PO";
    private static readonly string SupplierPaymentType = PaymentType.PAYMENT.ToString();

    public PurchaseOrderService(
        IRepository<StockMain> stockMainRepository,
        IRepository<TransactionType> transactionTypeRepository,
        IRepository<Payment> paymentRepository,
        IUnitOfWork unitOfWork)
    {
        _stockMainRepository = stockMainRepository;
        _transactionTypeRepository = transactionTypeRepository;
        _paymentRepository = paymentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<StockMain>> GetAllAsync()
    {
        var purchaseOrders = await _stockMainRepository.Query()
            .Include(s => s.TransactionType)
            .Include(s => s.Party)
            .Include(s => s.StockDetails)
            .Where(s => s.TransactionType!.Code == TRANSACTION_TYPE_CODE)
            .OrderByDescending(s => s.TransactionDate)
            .ThenByDescending(s => s.StockMainID)
            .ToListAsync();

        await RecalculateOutstandingAsync(purchaseOrders);
        return purchaseOrders;
    }

    public async Task<PagedResult<StockMain>> GetPagedAsync(int? supplierId, string? status, int page, int pageSize)
    {
        var query = _stockMainRepository.Query()
            .Include(s => s.TransactionType)
            .Include(s => s.Party)
            .Include(s => s.StockDetails)
            .Where(s => s.TransactionType!.Code == TRANSACTION_TYPE_CODE);

        if (supplierId.HasValue && supplierId.Value > 0)
            query = query.Where(s => s.Party_ID == supplierId.Value);

        if (!string.IsNullOrWhiteSpace(status) && status != "All")
            query = query.Where(s => s.Status == status);

        int totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(s => s.TransactionDate)
            .ThenByDescending(s => s.StockMainID)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Recalculate outstanding only for the current page's POs (per-PO independent).
        await RecalculateOutstandingAsync(items);

        return new PagedResult<StockMain>
        {
            Items = items,
            TotalCount = totalCount,
            CurrentPage = page,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// Read-only fetch for display. Deliberately NOT tracked: RecalculateOutstandingAsync below
    /// writes derived money fields onto the instance, and on a tracked entity those would be
    /// flushed to the database by whatever happens to call SaveChanges next in the same request.
    /// Write operations use <see cref="GetTrackedByIdAsync"/> instead.
    /// </summary>
    public async Task<StockMain?> GetByIdAsync(int id)
    {
        var purchaseOrder = await _stockMainRepository.Query()
            .AsNoTracking()
            .Include(s => s.TransactionType)
            .Include(s => s.Party)
            .Include(s => s.StockDetails)
                .ThenInclude(d => d.Product)
            .FirstOrDefaultAsync(s => s.StockMainID == id && s.TransactionType!.Code == TRANSACTION_TYPE_CODE);

        if (purchaseOrder != null)
        {
            await RecalculateOutstandingAsync(new List<StockMain> { purchaseOrder });
        }

        return purchaseOrder;
    }

    /// <summary>
    /// Tracked fetch used by the write operations in this service. No recalculation is applied,
    /// so nothing derived can ride along into an unrelated save.
    /// </summary>
    private async Task<StockMain?> GetTrackedByIdAsync(int id)
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
        purchaseOrder.CreatedAt = AppTime.Now;
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
        var existing = await GetTrackedByIdAsync(purchaseOrder.StockMainID);
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
        existing.UpdatedAt = AppTime.Now;
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
        var purchaseOrder = await GetTrackedByIdAsync(id);
        if (purchaseOrder == null)
            return false;

        if (purchaseOrder.Status != "Draft")
            return false;

        purchaseOrder.Status = "Approved";
        purchaseOrder.UpdatedAt = AppTime.Now;
        purchaseOrder.UpdatedBy = userId;

        _stockMainRepository.Update(purchaseOrder);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }

    public async Task<bool> ToggleStatusAsync(int id, int userId)
    {
        var purchaseOrder = await GetTrackedByIdAsync(id);
        if (purchaseOrder == null)
            return false;

        if (purchaseOrder.Status == "Void" || purchaseOrder.Status == "Completed")
            return false;

        // Block voiding if non-voided GRNs reference this PO
        var hasActiveGrns = await _stockMainRepository.Query()
            .Include(s => s.TransactionType)
            .AnyAsync(s => s.TransactionType!.Code == GRN_TRANSACTION_TYPE_CODE
                        && s.Status != "Void"
                        && s.ReferenceStockMain_ID == id);

        if (hasActiveGrns)
            throw new InvalidOperationException("Cannot cancel this Purchase Order because it has active GRN(s). Void the linked GRN(s) first.");

        // Block voiding if payments exist against this PO
        var hasPayments = await _paymentRepository.Query()
            .AnyAsync(p => p.StockMain_ID == id
                        && p.PaymentType == SupplierPaymentType
                        && !p.IsVoided);

        if (hasPayments)
            throw new InvalidOperationException("Cannot cancel this Purchase Order because it has active payment(s). Void the payment(s) first.");

        purchaseOrder.Status = "Void";
        purchaseOrder.VoidedAt = AppTime.Now;
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
        var datePrefix = DocumentNumberSequence.DatePrefix(PREFIX);
        await DocumentNumberSequence.SerializeAsync(_unitOfWork, datePrefix);

        var lastTransaction = await _stockMainRepository.Query()
            .Where(s => s.TransactionNo.StartsWith(datePrefix))
            .OrderByDescending(s => s.TransactionNo)
            .FirstOrDefaultAsync();

        return DocumentNumberSequence.Next(datePrefix, lastTransaction?.TransactionNo);
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

    /// <summary>
    /// Refreshes the DERIVED money fields (PaidAmount / BalanceAmount / PaymentStatus) on the
    /// supplied purchase orders from the authoritative Payment rows, for display.
    /// <para>
    /// This intentionally does NOT touch Status. Approved &lt;-&gt; Completed depends on received
    /// quantity, which only changes when a GRN is created, edited, or voided — so PurchaseService
    /// owns that transition and persists it deliberately. Deciding it here meant a read could
    /// leave an entity dirty and have the new status flushed by an unrelated later save.
    /// </para>
    /// </summary>
    private async Task RecalculateOutstandingAsync(IList<StockMain> purchaseOrders)
    {
        if (purchaseOrders.Count == 0)
        {
            return;
        }

        var activePos = purchaseOrders
            .Where(po => string.Equals(po.Status, "Approved", StringComparison.OrdinalIgnoreCase)
                      || string.Equals(po.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (activePos.Count == 0)
        {
            return;
        }

        var poIds = activePos.Select(po => po.StockMainID).ToList();

        var poPayments = await _paymentRepository.Query()
            .AsNoTracking()
            .Where(p => p.PaymentType == SupplierPaymentType
                     && !p.IsVoided
                     && p.StockMain_ID.HasValue
                     && poIds.Contains(p.StockMain_ID.Value))
            .GroupBy(p => p.StockMain_ID!.Value)
            .Select(g => new
            {
                PoId = g.Key,
                PaidAmount = g.Sum(p => p.Amount)
            })
            .ToListAsync();

        var paymentLookup = poPayments.ToDictionary(x => x.PoId, x => x.PaidAmount);

        foreach (var po in activePos)
        {
            paymentLookup.TryGetValue(po.StockMainID, out var paidAmount);

            po.PaidAmount = Math.Max(0, Math.Round(paidAmount, 2));
            po.BalanceAmount = Math.Max(0, Math.Round(po.TotalAmount - po.PaidAmount, 2));
            po.PaymentStatus = po.BalanceAmount <= 0
                ? PaymentStatus.Paid.ToString()
                : (po.PaidAmount <= 0 ? PaymentStatus.Unpaid.ToString() : PaymentStatus.Partial.ToString());
        }
    }

}
