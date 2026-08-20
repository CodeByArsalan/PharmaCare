using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Application.Utilities;
using PharmaCare.Domain.Entities.Accounting;

namespace PharmaCare.Application.Implementations.Accounting;

public class FinancialPeriodService : IFinancialPeriodService
{
    private readonly IRepository<FinancialPeriod> _periodRepository;
    private readonly IUnitOfWork _unitOfWork;

    public FinancialPeriodService(
        IRepository<FinancialPeriod> periodRepository,
        IUnitOfWork unitOfWork)
    {
        _periodRepository = periodRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> IsPeriodLockedAsync(DateTime date)
    {
        // Compare calendar days, not instants: EndDate is stored at midnight while postings carry
        // a time of day, so an instant-comparison leaves the whole of the period's last day open.
        var day = date.Date;
        return await _periodRepository.Query()
            .AnyAsync(p => p.IsClosed && day >= p.StartDate.Date && day <= p.EndDate.Date);
    }

    public async Task<IEnumerable<FinancialPeriod>> GetAllAsync()
    {
        return await _periodRepository.Query()
            .OrderByDescending(p => p.StartDate)
            .ToListAsync();
    }

    public async Task<FinancialPeriod?> GetByIdAsync(int id)
    {
        return await _periodRepository.GetByIdAsync(id);
    }

    public async Task<FinancialPeriod> CreateAsync(FinancialPeriod period, int userId)
    {
        // The name is the only label the period list and close screens show — a blank one leaves
        // the user closing "     ".
        if (string.IsNullOrWhiteSpace(period.Name))
            throw new InvalidOperationException("A period name is required.");
        period.Name = period.Name.Trim();

        if (period.StartDate > period.EndDate)
            throw new InvalidOperationException("A financial period cannot end before it starts.");

        // Standard interval intersection. Testing only whether the NEW period's start or end sits
        // inside an existing one misses the straddling case — a period spanning the whole of an
        // existing one has both endpoints outside it while overlapping it completely. Overlapping
        // periods make "is this date locked?" ambiguous, since the same date can then sit in one
        // closed period and one open one.
        var hasOverlap = await _periodRepository.Query()
            .AnyAsync(p => period.StartDate <= p.EndDate && period.EndDate >= p.StartDate);

        if (hasOverlap)
            throw new InvalidOperationException("This period overlaps with an existing financial period.");

        period.CreatedAt = AppTime.Now;
        period.CreatedBy = userId;
        period.IsClosed = false;

        await _periodRepository.AddAsync(period);
        await _unitOfWork.SaveChangesAsync();
        return period;
    }

    public async Task<bool> ClosePeriodAsync(int periodId, string? remarks, int userId)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            // Postings take this same lock for the span between their final period check and their
            // commit. Waiting on it here means a transaction already on its way to the ledger
            // finishes first, instead of landing in a period this call has just marked closed.
            await _unitOfWork.AcquireResourceLockAsync(AccountingConstants.PeriodCloseLockResource);

            var period = await _periodRepository.GetByIdAsync(periodId);
            if (period == null)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return false;
            }

            // Periods close in order. Closing declares a period's figures final — closing a LATER
            // one while an earlier one is still open means the earlier books can still change
            // after the later ones were signed off, and the two never reconcile.
            var earlierStillOpen = await _periodRepository.Query()
                .AnyAsync(p => !p.IsClosed && p.PeriodID != periodId && p.EndDate < period.StartDate);

            if (earlierStillOpen)
            {
                throw new InvalidOperationException(
                    "An earlier financial period is still open. Close periods in order, oldest first.");
            }

            period.IsClosed = true;
            period.ClosedAt = AppTime.Now;
            period.ClosedBy = userId;
            period.Remarks = remarks;
            period.UpdatedAt = AppTime.Now;
            period.UpdatedBy = userId;

            _periodRepository.Update(period);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();
            return true;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<bool> OpenPeriodAsync(int periodId, int userId)
    {
        var period = await _periodRepository.GetByIdAsync(periodId);
        if (period == null) return false;

        period.IsClosed = false;
        period.ClosedAt = null;
        period.ClosedBy = null;
        period.UpdatedAt = AppTime.Now;
        period.UpdatedBy = userId;

        _periodRepository.Update(period);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }
}
