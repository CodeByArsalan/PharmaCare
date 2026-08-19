using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Domain.Entities.Transactions;
using PharmaCare.Domain.Entities.Finance;
using PharmaCare.Application.Utilities;

namespace PharmaCare.Application.Implementations.Finance;

/// <summary>
/// Abstract base service for accounting operations.
/// </summary>
public abstract class BaseAccountingService
{
    protected readonly IUnitOfWork _unitOfWork;
    protected readonly IRepository<Voucher> _voucherRepository;
    private readonly IFinancialPeriodService? _periodService;

    protected BaseAccountingService(
        IUnitOfWork unitOfWork,
        IRepository<Voucher> voucherRepository,
        IFinancialPeriodService? periodService = null)
    {
        _unitOfWork = unitOfWork;
        _voucherRepository = voucherRepository;
        _periodService = periodService;
    }

    /// <summary>
    /// The posting date to re-check just before commit. Subclasses set this when they validate the
    /// period up front, so the check can be repeated under a lock once the work is done.
    /// </summary>
    private DateTime? _periodDateToRecheck;

    /// <summary>
    /// Records a posting date that has just passed its period check, so
    /// <see cref="ExecuteInTransactionAsync{T}"/> re-checks it under the close lock before
    /// committing. Without the second check a period closed mid-flight still accepts the posting.
    /// </summary>
    protected void TrackPeriodDate(DateTime date) => _periodDateToRecheck = date;

    private async Task RecheckPeriodBeforeCommitAsync()
    {
        if (_periodService is null || _periodDateToRecheck is not { } date) return;

        await _unitOfWork.AcquireResourceLockAsync(AccountingConstants.PeriodCloseLockResource);

        if (await _periodService.IsPeriodLockedAsync(date))
        {
            throw new InvalidOperationException(
                $"The financial period covering {date:dd/MM/yyyy} was closed while this transaction was being saved. It has not been posted.");
        }
    }

    /// <summary>
    /// Generates a unique reference number for a payment/receipt.
    /// Format: [PREFIX]-[YYYYMMDD]-[SEQ]
    /// </summary>
    protected async Task<string> GenerateReferenceNoAsync(IRepository<Payment> paymentRepository, string prefix)
    {
        var datePrefix = DocumentNumberSequence.DatePrefix(prefix);
        await DocumentNumberSequence.SerializeAsync(_unitOfWork, datePrefix);

        var lastPayment = await paymentRepository.Query()
            .Where(p => p.Reference != null && p.Reference.StartsWith(datePrefix))
            .OrderByDescending(p => p.Reference)
            .FirstOrDefaultAsync();

        return DocumentNumberSequence.Next(datePrefix, lastPayment?.Reference);
    }

    /// <summary>
    /// Generates a unique voucher number based on a prefix.
    /// Format: [PREFIX]-[YYYYMMDD]-[SEQ]
    /// </summary>
    protected async Task<string> GenerateVoucherNoAsync(string prefix)
    {
        return await _voucherRepository.GenerateVoucherNoAsync(prefix, _unitOfWork);
    }

    /// <summary>
    /// Executes a series of operations within a database transaction.
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
            _periodDateToRecheck = null;
        }
    }

    /// <summary>
    /// Creates a reversal voucher for an existing voucher.
    /// </summary>
    protected async Task<Voucher?> CreateVoucherReversalAsync(int originalVoucherId, int userId, string reason)
    {
        return await _voucherRepository.CreateReversalVoucherAsync(originalVoucherId, userId, reason);
    }
}
