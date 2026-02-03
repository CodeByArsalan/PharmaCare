using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.Application.Implementations.Transactions;

/// <summary>
/// Service for managing Sale Returns.
/// </summary>
public class SaleReturnService : ISaleReturnService
{
    private readonly IRepository<StockMain> _stockMainRepository;
    private readonly IRepository<TransactionType> _transactionTypeRepository;
    private readonly IUnitOfWork _unitOfWork;

    private const string TRANSACTION_TYPE_CODE = "SRTN";
    private const string SALE_TRANSACTION_TYPE_CODE = "SALE";
    private const string PREFIX = "SRTN";

    public SaleReturnService(
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

    public async Task<StockMain> CreateAsync(StockMain saleReturn, int userId)
    {
        // Get the SRTN transaction type
        var transactionType = await _transactionTypeRepository.Query()
            .FirstOrDefaultAsync(t => t.Code == TRANSACTION_TYPE_CODE);

        if (transactionType == null)
            throw new InvalidOperationException($"Transaction type '{TRANSACTION_TYPE_CODE}' not found.");

        saleReturn.TransactionType_ID = transactionType.TransactionTypeID;
        saleReturn.TransactionNo = await GenerateTransactionNoAsync();
        saleReturn.Status = "Approved"; // Returns are immediately approved (stock impact)
        saleReturn.PaymentStatus = "Unpaid";
        saleReturn.CreatedAt = DateTime.Now;
        saleReturn.CreatedBy = userId;

        // Calculate totals
        CalculateTotals(saleReturn);

        await _stockMainRepository.AddAsync(saleReturn);

        // Update the reference Sale's balance if linked
        if (saleReturn.ReferenceStockMain_ID.HasValue)
        {
            var sale = await _stockMainRepository.Query()
                .FirstOrDefaultAsync(s => s.StockMainID == saleReturn.ReferenceStockMain_ID.Value);

            if (sale != null)
            {
                sale.BalanceAmount -= saleReturn.TotalAmount;
                _stockMainRepository.Update(sale);
            }
        }

        await _unitOfWork.SaveChangesAsync();

        return saleReturn;
    }

    public async Task<IEnumerable<StockMain>> GetSalesForReturnAsync(int? customerId = null)
    {
        var query = _stockMainRepository.Query()
            .Include(s => s.TransactionType)
            .Include(s => s.Party)
            .Include(s => s.StockDetails)
                .ThenInclude(d => d.Product)
            .Where(s => s.TransactionType!.Code == SALE_TRANSACTION_TYPE_CODE 
                     && s.Status == "Approved"
                     && s.BalanceAmount > 0);

        if (customerId.HasValue)
        {
            query = query.Where(s => s.Party_ID == customerId.Value);
        }

        return await query
            .OrderByDescending(s => s.TransactionDate)
            .ToListAsync();
    }

    public async Task<bool> VoidAsync(int id, string reason, int userId)
    {
        var saleReturn = await GetByIdAsync(id);
        if (saleReturn == null)
            return false;

        if (saleReturn.Status == "Void")
            return false;

        saleReturn.Status = "Void";
        saleReturn.VoidReason = reason;
        saleReturn.VoidedAt = DateTime.Now;
        saleReturn.VoidedBy = userId;

        // Restore the reference Sale's balance if linked
        if (saleReturn.ReferenceStockMain_ID.HasValue)
        {
            var sale = await _stockMainRepository.Query()
                .FirstOrDefaultAsync(s => s.StockMainID == saleReturn.ReferenceStockMain_ID.Value);

            if (sale != null)
            {
                sale.BalanceAmount += saleReturn.TotalAmount;
                _stockMainRepository.Update(sale);
            }
        }

        _stockMainRepository.Update(saleReturn);
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

    private void CalculateTotals(StockMain saleReturn)
    {
        saleReturn.SubTotal = saleReturn.StockDetails.Sum(d => d.LineTotal);

        if (saleReturn.DiscountPercent > 0)
        {
            saleReturn.DiscountAmount = Math.Round(saleReturn.SubTotal * saleReturn.DiscountPercent / 100, 2);
        }

        saleReturn.TotalAmount = saleReturn.SubTotal - saleReturn.DiscountAmount;
        saleReturn.BalanceAmount = saleReturn.TotalAmount - saleReturn.PaidAmount;
    }
}
