using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces;
using PharmaCare.Domain.Entities.Accounting;
using PharmaCare.Domain.Entities.Transactions;

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

    protected TransactionServiceBase(
        IRepository<StockMain> stockMainRepository,
        IRepository<Voucher> voucherRepository,
        IUnitOfWork unitOfWork)
    {
        _stockMainRepository = stockMainRepository;
        _voucherRepository = voucherRepository;
        _unitOfWork = unitOfWork;
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
        var datePrefix = $"{prefix}-{DateTime.Now:yyyyMMdd}-";

        var lastVoucher = await _voucherRepository.Query()
            .Where(v => v.VoucherNo.StartsWith(datePrefix))
            .OrderByDescending(v => v.VoucherNo)
            .FirstOrDefaultAsync();

        int nextNum = 1;
        if (lastVoucher != null)
        {
            var parts = lastVoucher.VoucherNo.Split('-');
            if (parts.Length > 2 && int.TryParse(parts.Last(), out int lastNum))
            {
                nextNum = lastNum + 1;
            }
        }

        return $"{datePrefix}{nextNum:D4}";
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

        stockMain.TotalAmount = stockMain.SubTotal - stockMain.DiscountAmount;
        stockMain.BalanceAmount = stockMain.TotalAmount - stockMain.PaidAmount;
    }

    /// <summary>
    /// Creates a reversal voucher for a voided transaction.
    /// Reverses the original voucher by swapping Debits and Credits.
    /// </summary>
    protected async Task<Voucher?> CreateReversalVoucherAsync(int? originalVoucherId, int userId, string voidReason, string stockMainIdSource = "StockMain", int? stockMainId = null)
    {
        if (!originalVoucherId.HasValue)
            return null;

        var originalVoucher = await _voucherRepository.Query()
            .Include(v => v.VoucherDetails)
            .FirstOrDefaultAsync(v => v.VoucherID == originalVoucherId.Value);

        if (originalVoucher == null || originalVoucher.IsReversed)
            return null;

        // Use the same prefix as the original but maybe ensure uniqueness if needed, 
        // OR simply reuse the logic. Usually Reversal Vouchers might have a specific prefix purely for ID,
        // but often we just use the same series or a specific "REV" prefix.
        // Looking at SaleService: "REV-{originalVoucher.VoucherNo}"
        // Looking at ReturnServices: New number with same prefix.
        
        // Let's stick to the styling found in SaleService ("REV-...") as it's clearer
        // OR standard new number. 
        // SaleService uses: `var voucherNo = $"REV-{originalVoucher.VoucherNo}";`
        // SaleReturnService uses: `GenerateVoucherNoAsync(SALE_RETURN_VOUCHER_CODE)`
        
        // I will use REV- prefix for clarity and to avoid burning sequence numbers if not needed.
        var voucherNo = $"REV-{originalVoucher.VoucherNo}";

        var reversalVoucher = new Voucher
        {
            VoucherType_ID = originalVoucher.VoucherType_ID,
            VoucherNo = voucherNo,
            VoucherDate = DateTime.Now,
            TotalDebit = originalVoucher.TotalCredit, // Swapped
            TotalCredit = originalVoucher.TotalDebit, // Swapped
            Status = "Posted",
            SourceTable = stockMainIdSource,
            SourceID = stockMainId ?? originalVoucher.SourceID,
            Narration = $"Reversal of {originalVoucher.VoucherNo} - Void: {voidReason}",
            ReversesVoucher_ID = originalVoucher.VoucherID,
            CreatedAt = DateTime.Now,
            CreatedBy = userId
        };

        foreach (var detail in originalVoucher.VoucherDetails)
        {
            reversalVoucher.VoucherDetails.Add(new VoucherDetail
            {
                Account_ID = detail.Account_ID,
                DebitAmount = detail.CreditAmount,   // Swap: original credit becomes debit
                CreditAmount = detail.DebitAmount,   // Swap: original debit becomes credit
                Description = $"Reversal - {detail.Description}",
                Party_ID = detail.Party_ID,
                Product_ID = detail.Product_ID
            });
        }

        await _voucherRepository.AddAsync(reversalVoucher);

        // Mark original voucher as reversed and link it to the generated reversal voucher.
        originalVoucher.IsReversed = true;
        originalVoucher.ReversedByVoucher = reversalVoucher;
        _voucherRepository.Update(originalVoucher);
        await _unitOfWork.SaveChangesAsync();

        return reversalVoucher;
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
