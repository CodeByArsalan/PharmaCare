using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces;
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

    protected BaseAccountingService(IUnitOfWork unitOfWork, IRepository<Voucher> voucherRepository)
    {
        _unitOfWork = unitOfWork;
        _voucherRepository = voucherRepository;
    }

    /// <summary>
    /// Generates a unique reference number for a payment/receipt.
    /// Format: [PREFIX]-[YYYYMMDD]-[SEQ]
    /// </summary>
    protected async Task<string> GenerateReferenceNoAsync(IRepository<Payment> paymentRepository, string prefix)
    {
        var datePrefix = $"{prefix}-{DateTime.Now:yyyyMMdd}-";

        var lastPayment = await paymentRepository.Query()
            .Where(p => p.Reference != null && p.Reference.StartsWith(datePrefix))
            .OrderByDescending(p => p.Reference)
            .FirstOrDefaultAsync();

        int nextNum = 1;
        if (lastPayment != null && lastPayment.Reference != null)
        {
            var parts = lastPayment.Reference.Split('-');
            if (parts.Length > 2 && int.TryParse(parts.Last(), out int lastNum))
            {
                nextNum = lastNum + 1;
            }
        }

        return $"{datePrefix}{nextNum:D4}";
    }

    /// <summary>
    /// Generates a unique voucher number based on a prefix.
    /// Format: [PREFIX]-[YYYYMMDD]-[SEQ]
    /// </summary>
    protected async Task<string> GenerateVoucherNoAsync(string prefix)
    {
        return await _voucherRepository.GenerateVoucherNoAsync(prefix);
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
    /// Creates a reversal voucher for an existing voucher.
    /// </summary>
    protected async Task<Voucher?> CreateVoucherReversalAsync(int originalVoucherId, int userId, string reason)
    {
        return await _voucherRepository.CreateReversalVoucherAsync(originalVoucherId, userId, reason);
    }
}
