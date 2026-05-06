using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Domain.Entities.Accounting;
using PharmaCare.Domain.Entities.Transactions;
using PharmaCare.Application.Utilities;

namespace PharmaCare.Application.Implementations.Transactions;

/// <summary>
/// Abstract base class for transaction services (Sale, Purchase, Returns).
/// Contains common logic for generating numbers, calculating totals, and managing vouchers.
/// </summary>
public abstract class TransactionServiceBase
{
    protected readonly IRepository<StockMain> _stockMainRepository;
    protected readonly IRepository<Voucher> _voucherRepository;
    protected readonly IUnitOfWork _unitOfWork;
    protected readonly IFinancialPeriodService _financialPeriodService;

    protected TransactionServiceBase(
        IRepository<StockMain> stockMainRepository,
        IRepository<Voucher> voucherRepository,
        IUnitOfWork unitOfWork,
        IFinancialPeriodService financialPeriodService)
    {
        _stockMainRepository = stockMainRepository;
        _voucherRepository = voucherRepository;
        _unitOfWork = unitOfWork;
        _financialPeriodService = financialPeriodService;
    }

    /// <summary>
    /// Validates that the transaction date is not within a closed financial period.
    /// </summary>
    protected async Task ValidatePeriodAsync(DateTime date)
    {
        if (await _financialPeriodService.IsPeriodLockedAsync(date))
        {
            throw new InvalidOperationException($"The date {date:dd/MM/yyyy} falls within a closed financial period. Transactions are locked.");
        }
    }

    /// <summary>
    /// Executes a multi-entity operation inside a single database transaction.
    /// </summary>
    protected async Task ExecuteInTransactionAsync(Func<Task> operation)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            await operation();
            await _unitOfWork.CommitTransactionAsync();
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    /// <summary>
    /// Executes a multi-entity operation inside a single database transaction and returns a result.
    /// </summary>
    protected async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var result = await operation();
            await _unitOfWork.CommitTransactionAsync();
            return result;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    /// <summary>
    /// Generates a new transaction number in the format PREFIX-YYYYMMDD-XXXX.
    /// </summary>
    protected async Task<string> GenerateTransactionNoAsync(string prefix)
    {
        var datePrefix = $"{prefix}-{DateTime.Now:yyyyMMdd}-";

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

    /// <summary>
    /// Generates a new voucher number in the format PREFIX-YYYYMMDD-XXXX.
    /// </summary>
    protected async Task<string> GenerateVoucherNoAsync(string prefix)
    {
        return await _voucherRepository.GenerateVoucherNoAsync(prefix);
    }

    /// <summary>
    /// Calculates SubTotal, DiscountAmount, TotalAmount, and BalanceAmount for a transaction.
    /// </summary>
    protected void CalculateTotals(StockMain stockMain)
    {
        stockMain.SubTotal = stockMain.StockDetails.Sum(d => d.LineTotal);

        if (stockMain.DiscountPercent > 0)
        {
            stockMain.DiscountAmount = Math.Round(stockMain.SubTotal * stockMain.DiscountPercent / 100, 2);
        }
        else
        {
            // Reset discount amount if percent is 0 to prevent spoofing
            stockMain.DiscountAmount = 0;
        }

        stockMain.TotalAmount = stockMain.SubTotal - stockMain.DiscountAmount;
        stockMain.BalanceAmount = stockMain.TotalAmount - stockMain.PaidAmount;
    }

    /// <summary>
    /// Creates a reversal voucher for a voided transaction.
    /// Reverses the original voucher by swapping Debits and Credits.
    /// </summary>
    protected async Task<Voucher?> CreateReversalVoucherAsync(int? originalVoucherId, int userId, string voidReason, string stockMainIdSource = "StockMain", int? stockMainId = null)
    {
        if (!originalVoucherId.HasValue) return null;
        var reversal = await _voucherRepository.CreateReversalVoucherAsync(originalVoucherId.Value, userId, voidReason, stockMainIdSource, stockMainId);
        if (reversal != null)
        {
            await _unitOfWork.SaveChangesAsync();
        }
        return reversal;
    }

    /// <summary>
    /// Creates reversal vouchers for all posted vouchers linked to the same source record.
    /// This supports multi-voucher transactions (e.g. invoice + payment vouchers on one StockMain).
    /// </summary>
    protected async Task<int> CreateReversalVouchersForSourceAsync(string sourceTable, int sourceId, int userId, string voidReason)
    {
        var voucherIds = await _voucherRepository.Query()
            .Where(v => v.SourceTable == sourceTable
                        && v.SourceID == sourceId
                        && v.Status == "Posted"
                        && !v.IsReversed
                        && v.ReversesVoucher_ID == null)
            .OrderBy(v => v.VoucherID)
            .Select(v => v.VoucherID)
            .ToListAsync();

        var reversedCount = 0;
        foreach (var voucherId in voucherIds)
        {
            var reversal = await CreateReversalVoucherAsync(voucherId, userId, voidReason, sourceTable, sourceId);
            if (reversal != null)
            {
                reversedCount++;
            }
        }

        return reversedCount;
    }
}
