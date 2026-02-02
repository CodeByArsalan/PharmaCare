using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.Application.Implementations.Transactions;

/// <summary>
/// Service for managing Purchases (GRN - Goods Received Notes).
/// </summary>
public class PurchaseService : IPurchaseService
{
    private readonly IRepository<StockMain> _stockMainRepository;
    private readonly IRepository<TransactionType> _transactionTypeRepository;
    private readonly IUnitOfWork _unitOfWork;

    private const string TRANSACTION_TYPE_CODE = "GRN";
    private const string PO_TRANSACTION_TYPE_CODE = "PO";
    private const string PREFIX = "GRN";

    public PurchaseService(
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
            .Include(s => s.ReferenceStockMain)
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
            .Include(s => s.ReferenceStockMain)
            .Include(s => s.StockDetails)
                .ThenInclude(d => d.Product)
            .FirstOrDefaultAsync(s => s.StockMainID == id && s.TransactionType!.Code == TRANSACTION_TYPE_CODE);
    }

    public async Task<StockMain> CreateAsync(StockMain purchase, int userId)
    {
        // Get the GRN transaction type
        var transactionType = await _transactionTypeRepository.Query()
            .FirstOrDefaultAsync(t => t.Code == TRANSACTION_TYPE_CODE);

        if (transactionType == null)
            throw new InvalidOperationException($"Transaction type '{TRANSACTION_TYPE_CODE}' not found.");

        purchase.TransactionType_ID = transactionType.TransactionTypeID;
        purchase.TransactionNo = await GenerateTransactionNoAsync();
        purchase.Status = "Approved"; // GRN is immediately approved (stock impact)
        purchase.PaymentStatus = "Unpaid";
        purchase.CreatedAt = DateTime.Now;
        purchase.CreatedBy = userId;

        // Calculate totals
        CalculateTotals(purchase);

        await _stockMainRepository.AddAsync(purchase);
        await _unitOfWork.SaveChangesAsync();

        return purchase;
    }

    public async Task<IEnumerable<StockMain>> GetPurchaseOrdersForGrnAsync(int? supplierId = null)
    {
        var query = _stockMainRepository.Query()
            .Include(s => s.TransactionType)
            .Include(s => s.Party)
            .Include(s => s.StockDetails)
                .ThenInclude(d => d.Product)
            .Where(s => s.TransactionType!.Code == PO_TRANSACTION_TYPE_CODE && s.Status == "Approved");

        if (supplierId.HasValue)
        {
            query = query.Where(s => s.Party_ID == supplierId.Value);
        }

        return await query
            .OrderByDescending(s => s.TransactionDate)
            .ToListAsync();
    }

    public async Task<bool> VoidAsync(int id, string reason, int userId)
    {
        var purchase = await GetByIdAsync(id);
        if (purchase == null)
            return false;

        if (purchase.Status == "Void")
            return false;

        purchase.Status = "Void";
        purchase.VoidReason = reason;
        purchase.VoidedAt = DateTime.Now;
        purchase.VoidedBy = userId;

        _stockMainRepository.Update(purchase);
        await _unitOfWork.SaveChangesAsync();

        return true;
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

    private void CalculateTotals(StockMain purchase)
    {
        purchase.SubTotal = purchase.StockDetails.Sum(d => d.LineTotal);

        if (purchase.DiscountPercent > 0)
        {
            purchase.DiscountAmount = Math.Round(purchase.SubTotal * purchase.DiscountPercent / 100, 2);
        }

        purchase.TotalAmount = purchase.SubTotal - purchase.DiscountAmount;
        purchase.BalanceAmount = purchase.TotalAmount - purchase.PaidAmount;
    }
}
