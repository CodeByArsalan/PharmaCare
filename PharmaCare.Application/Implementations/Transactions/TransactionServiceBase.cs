using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Application.Interfaces.Configuration;
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
    /// EVERY date passed to <see cref="ValidatePeriodAsync"/> during the operation, re-checked just
    /// before the surrounding transaction commits. A set, not a single slot: an edit validates both
    /// the original date (whose voucher is being reversed) and the new date, and a period closing
    /// concurrently over either must abort the commit. Scoped per request with the service instance.
    /// </summary>
    private readonly HashSet<DateTime> _periodDatesToRecheck = new();

    /// <summary>
    /// Validates that the transaction date is not within a closed financial period.
    /// <para>
    /// The date is remembered so <see cref="ExecuteInTransactionAsync{T}"/> can re-check it under a
    /// lock immediately before committing. This check alone is not enough: it runs before (or early
    /// in) the transaction, so a period closed while the posting is still in flight would otherwise
    /// let the posting land in it.
    /// </para>
    /// </summary>
    protected async Task ValidatePeriodAsync(DateTime date)
    {
        // Trading documents record what has happened. A date beyond today posts into a period
        // that cannot be closed ahead of it and skews every date-ranged report; nothing here is
        // legitimately dated in the future (post-dated cheques carry their own guard).
        if (date.Date > AppTime.Now.Date)
        {
            throw new InvalidOperationException("The transaction date cannot be in the future.");
        }

        if (await _financialPeriodService.IsPeriodLockedAsync(date))
        {
            throw new InvalidOperationException($"The date {date:dd/MM/yyyy} falls within a closed financial period. Transactions are locked.");
        }

        _periodDatesToRecheck.Add(date.Date);
    }

    /// <summary>
    /// Re-checks every remembered posting date immediately before commit, holding the same lock
    /// <c>ClosePeriodAsync</c> takes. Closing therefore waits behind a posting that is already
    /// committing, and a posting reaching this point after a close has committed rolls back.
    /// </summary>
    private async Task RecheckPeriodBeforeCommitAsync()
    {
        if (_periodDatesToRecheck.Count == 0) return;

        await _unitOfWork.AcquireResourceLockAsync(AccountingConstants.PeriodCloseLockResource);

        foreach (var date in _periodDatesToRecheck)
        {
            if (await _financialPeriodService.IsPeriodLockedAsync(date))
            {
                throw new InvalidOperationException(
                    $"The financial period covering {date:dd/MM/yyyy} was closed while this transaction was being saved. It has not been posted.");
            }
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
            await RecheckPeriodBeforeCommitAsync();
            await _unitOfWork.CommitTransactionAsync();
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
        finally
        {
            // Never let one operation's date leak into the next on this scoped instance.
            _periodDatesToRecheck.Clear();
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
            await RecheckPeriodBeforeCommitAsync();
            await _unitOfWork.CommitTransactionAsync();
            return result;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
        finally
        {
            // Never let one operation's date leak into the next on this scoped instance.
            _periodDatesToRecheck.Clear();
        }
    }

    /// <summary>
    /// Serializes concurrent stock-affecting transactions on the given products. Stock-on-hand
    /// is computed from movement history, so availability checks are read-then-write and can be
    /// raced past by parallel transactions; taking these transaction-scoped locks first makes the
    /// check safe. Must be called inside ExecuteInTransactionAsync, before reading stock.
    /// </summary>
    protected Task LockProductStockAsync(IEnumerable<int> productIds)
        => _unitOfWork.AcquireResourceLocksAsync("product-stock", productIds);

    /// <summary>
    /// Locks the given products, then verifies that removing <paramref name="removalByProduct"/>
    /// units (all values positive) leaves no product with negative stock-on-hand. Used by every
    /// operation that takes stock OUT — sales are covered separately, but voids/edits that unwind
    /// received or returned stock must also prove the goods are still on the shelf.
    /// Must be called inside ExecuteInTransactionAsync.
    /// </summary>
    protected async Task EnsureRemovalLeavesNonNegativeStockAsync(
        IProductService productService,
        Dictionary<int, decimal> removalByProduct,
        string operationLabel)
    {
        if (removalByProduct.Count == 0) return;

        var productIds = removalByProduct.Keys.ToList();
        await LockProductStockAsync(productIds);

        var stockNow = await productService.GetStockStatusAsync(productIds);
        foreach (var (productId, removal) in removalByProduct)
        {
            if (removal <= 0) continue;

            var onHand = stockNow.TryGetValue(productId, out var s) ? s : 0;
            if (removal > onHand)
            {
                throw new InvalidOperationException(
                    $"Cannot {operationLabel}: product ID {productId} has {onHand} in stock but this would remove {removal}. " +
                    "Stock would go negative — void or adjust the dependent transactions first.");
            }
        }
    }

    /// <summary>
    /// Generates a new transaction number in the format PREFIX-YYYYMMDD-XXXX.
    /// </summary>
    protected async Task<string> GenerateTransactionNoAsync(string prefix)
    {
        var datePrefix = DocumentNumberSequence.DatePrefix(prefix);
        await DocumentNumberSequence.SerializeAsync(_unitOfWork, datePrefix);

        var lastTransaction = await _stockMainRepository.Query()
            .Where(s => s.TransactionNo.StartsWith(datePrefix))
            // Length before value — a plain string sort puts "-10000" below "-9999".
            .OrderByDescending(s => s.TransactionNo.Length)
            .ThenByDescending(s => s.TransactionNo)
            .FirstOrDefaultAsync();

        return DocumentNumberSequence.Next(datePrefix, lastTransaction?.TransactionNo);
    }

    /// <summary>
    /// Generates a new voucher number in the format PREFIX-YYYYMMDD-XXXX.
    /// </summary>
    protected async Task<string> GenerateVoucherNoAsync(string prefix)
    {
        return await _voucherRepository.GenerateVoucherNoAsync(prefix, _unitOfWork);
    }

    /// <summary>
    /// Calculates SubTotal, DiscountAmount, TotalAmount, and BalanceAmount for a transaction.
    /// </summary>
    protected void CalculateTotals(StockMain stockMain)
    {
        // The web VM range-checks this, but the service is the authoritative gate for every
        // caller: a percent outside 0–100 would drive TotalAmount (and then PaidAmount via the
        // tendered-amount cap) negative and post an inverted voucher.
        if (stockMain.DiscountPercent < 0 || stockMain.DiscountPercent > 100)
            throw new InvalidOperationException("Discount percent must be between 0 and 100.");

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

        // One ceiling for every trading document, applied at the single point where they all
        // arrive at a total. It used to guard expenses and journal vouchers only, which left sales,
        // purchases and returns — the documents carrying the most value — with none at all.
        TransactionAmounts.EnsureWithinSanityCap(stockMain.TotalAmount, "Transaction");
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
