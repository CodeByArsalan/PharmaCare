using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Accounting;
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
        // Check if any closed period covers this date
        return await _periodRepository.Query()
            .AnyAsync(p => p.IsClosed && date >= p.StartDate && date <= p.EndDate);
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
        // Validate no overlap with existing periods (basic check)
        var hasOverlap = await _periodRepository.Query()
            .AnyAsync(p => (period.StartDate >= p.StartDate && period.StartDate <= p.EndDate) ||
                           (period.EndDate >= p.StartDate && period.EndDate <= p.EndDate));

        if (hasOverlap)
            throw new InvalidOperationException("This period overlaps with an existing financial period.");

        period.CreatedAt = DateTime.Now;
        period.CreatedBy = userId;
        period.IsClosed = false;

        await _periodRepository.AddAsync(period);
        await _unitOfWork.SaveChangesAsync();
        return period;
    }

    public async Task<bool> ClosePeriodAsync(int periodId, string? remarks, int userId)
    {
        var period = await _periodRepository.GetByIdAsync(periodId);
        if (period == null) return false;

        period.IsClosed = true;
        period.ClosedAt = DateTime.Now;
        period.ClosedBy = userId;
        period.Remarks = remarks;
        period.UpdatedAt = DateTime.Now;
        period.UpdatedBy = userId;

        _periodRepository.Update(period);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<bool> OpenPeriodAsync(int periodId, int userId)
    {
        var period = await _periodRepository.GetByIdAsync(periodId);
        if (period == null) return false;

        period.IsClosed = false;
        period.ClosedAt = null;
        period.ClosedBy = null;
        period.UpdatedAt = DateTime.Now;
        period.UpdatedBy = userId;

        _periodRepository.Update(period);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }
}
