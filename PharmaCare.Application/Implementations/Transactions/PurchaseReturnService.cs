using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.Application.Implementations.Transactions;

/// <summary>
/// Service for managing Purchase Returns.
/// </summary>
public class PurchaseReturnService : IPurchaseReturnService
{
    private readonly IRepository<StockMain> _stockMainRepository;
    private readonly IRepository<TransactionType> _transactionTypeRepository;
    private readonly IUnitOfWork _unitOfWork;

    private const string TRANSACTION_TYPE_CODE = "PRTN";
    private const string GRN_TRANSACTION_TYPE_CODE = "GRN";
    private const string PREFIX = "PRTN";

    public PurchaseReturnService(
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

    public async Task<StockMain> CreateAsync(StockMain purchaseReturn, int userId)
    {
        // Get the PRTN transaction type
        var transactionType = await _transactionTypeRepository.Query()
            .FirstOrDefaultAsync(t => t.Code == TRANSACTION_TYPE_CODE);

        if (transactionType == null)
            throw new InvalidOperationException($"Transaction type '{TRANSACTION_TYPE_CODE}' not found.");

        purchaseReturn.TransactionType_ID = transactionType.TransactionTypeID;
        purchaseReturn.TransactionNo = await GenerateTransactionNoAsync();
        purchaseReturn.Status = "Approved"; // Returns are immediately approved (stock impact)
        purchaseReturn.PaymentStatus = "Unpaid";
        purchaseReturn.CreatedAt = DateTime.Now;
        purchaseReturn.CreatedBy = userId;

        // Calculate totals
        CalculateTotals(purchaseReturn);

        await _stockMainRepository.AddAsync(purchaseReturn);

        // Update the reference GRN's balance if linked
        if (purchaseReturn.ReferenceStockMain_ID.HasValue)
        {
            var grn = await _stockMainRepository.Query()
                .FirstOrDefaultAsync(s => s.StockMainID == purchaseReturn.ReferenceStockMain_ID.Value);

            if (grn != null)
            {
                grn.BalanceAmount -= purchaseReturn.TotalAmount;
                _stockMainRepository.Update(grn);
            }
        }

        await _unitOfWork.SaveChangesAsync();

        return purchaseReturn;
    }

    public async Task<IEnumerable<StockMain>> GetGrnsForReturnAsync(int? supplierId = null)
    {
        var query = _stockMainRepository.Query()
            .Include(s => s.TransactionType)
            .Include(s => s.Party)
            .Include(s => s.StockDetails)
                .ThenInclude(d => d.Product)
            .Where(s => s.TransactionType!.Code == GRN_TRANSACTION_TYPE_CODE 
                     && s.Status == "Approved"
                     && s.BalanceAmount > 0);

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
        var purchaseReturn = await GetByIdAsync(id);
        if (purchaseReturn == null)
            return false;

        if (purchaseReturn.Status == "Void")
            return false;

        purchaseReturn.Status = "Void";
        purchaseReturn.VoidReason = reason;
        purchaseReturn.VoidedAt = DateTime.Now;
        purchaseReturn.VoidedBy = userId;

        // Restore the reference GRN's balance if linked
        if (purchaseReturn.ReferenceStockMain_ID.HasValue)
        {
            var grn = await _stockMainRepository.Query()
                .FirstOrDefaultAsync(s => s.StockMainID == purchaseReturn.ReferenceStockMain_ID.Value);

            if (grn != null)
            {
                grn.BalanceAmount += purchaseReturn.TotalAmount;
                _stockMainRepository.Update(grn);
            }
        }

        _stockMainRepository.Update(purchaseReturn);
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

    private void CalculateTotals(StockMain purchaseReturn)
    {
        purchaseReturn.SubTotal = purchaseReturn.StockDetails.Sum(d => d.LineTotal);

        if (purchaseReturn.DiscountPercent > 0)
        {
            purchaseReturn.DiscountAmount = Math.Round(purchaseReturn.SubTotal * purchaseReturn.DiscountPercent / 100, 2);
        }

        purchaseReturn.TotalAmount = purchaseReturn.SubTotal - purchaseReturn.DiscountAmount;
        purchaseReturn.BalanceAmount = purchaseReturn.TotalAmount - purchaseReturn.PaidAmount;
    }
}
