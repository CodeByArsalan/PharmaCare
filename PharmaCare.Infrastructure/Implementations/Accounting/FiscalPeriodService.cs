using Microsoft.EntityFrameworkCore;
using PharmaCare.Domain.Models.AccountManagement;
using PharmaCare.Infrastructure.Exceptions;
using PharmaCare.Infrastructure.Interfaces;
using PharmaCare.Infrastructure.Interfaces.Accounting;

namespace PharmaCare.Infrastructure.Implementations.Accounting;

/// <summary>
/// Service for managing fiscal periods and controlling accounting period access.
/// </summary>
public sealed class FiscalPeriodService : IFiscalPeriodService
{
    private readonly PharmaCareDBContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public FiscalPeriodService(PharmaCareDBContext context, IUnitOfWork unitOfWork)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    #region Period Queries

    public async Task<FiscalPeriod?> GetCurrentPeriodAsync(int? storeId = null, CancellationToken ct = default)
    {
        return await GetPeriodForDateAsync(DateTime.Today, storeId, ct);
    }

    public async Task<FiscalPeriod?> GetPeriodForDateAsync(DateTime date, int? storeId = null, CancellationToken ct = default)
    {
        var dateOnly = date.Date;

        return await _context.FiscalPeriods
            .Include(p => p.FiscalYear)
            .FirstOrDefaultAsync(p =>
                dateOnly >= p.StartDate.Date && dateOnly <= p.EndDate.Date, ct);
    }

    public async Task<List<FiscalPeriod>> GetPeriodsForYearAsync(int fiscalYearId, CancellationToken ct = default)
    {
        return await _context.FiscalPeriods
            .Where(p => p.FiscalYear_ID == fiscalYearId)
            .OrderBy(p => p.PeriodNumber)
            .ToListAsync(ct);
    }

    public async Task<FiscalPeriod?> GetPeriodByIdAsync(int periodId, CancellationToken ct = default)
    {
        return await _context.FiscalPeriods
            .Include(p => p.FiscalYear)
            .FirstOrDefaultAsync(p => p.FiscalPeriodID == periodId, ct);
    }

    public async Task<string> GetEffectiveStatusAsync(int periodId, int? storeId = null, CancellationToken ct = default)
    {
        var period = await GetPeriodByIdAsync(periodId, ct);
        if (period == null)
        {
            throw new InvalidOperationException($"Fiscal period {periodId} not found");
        }

        // Check store-specific override first
        if (storeId.HasValue)
        {
            var storeOverride = await _context.StoreFiscalPeriods
                .FirstOrDefaultAsync(sfp =>
                    sfp.FiscalPeriod_ID == periodId &&
                    sfp.Store_ID == storeId.Value, ct);

            if (storeOverride != null)
            {
                return storeOverride.Status;
            }
        }

        // Fall back to global period status
        return period.Status;
    }

    #endregion

    #region Period Validation

    public async Task ValidatePeriodOpenAsync(DateTime postingDate, int? storeId = null, CancellationToken ct = default)
    {
        var period = await GetPeriodForDateAsync(postingDate, storeId, ct);

        if (period == null)
        {
            throw new FiscalPeriodClosedException($"No fiscal period found for date {postingDate:yyyy-MM-dd}");
        }

        var effectiveStatus = await GetEffectiveStatusAsync(period.FiscalPeriodID, storeId, ct);

        if (effectiveStatus != "Open")
        {
            throw new FiscalPeriodClosedException(
                $"Fiscal period {period.PeriodCode} ({period.PeriodName}) is {effectiveStatus}. " +
                $"Posting date: {postingDate:yyyy-MM-dd}");
        }
    }

    public async Task<bool> IsPeriodOpenAsync(DateTime postingDate, int? storeId = null, CancellationToken ct = default)
    {
        try
        {
            await ValidatePeriodOpenAsync(postingDate, storeId, ct);
            return true;
        }
        catch (FiscalPeriodClosedException)
        {
            return false;
        }
    }

    #endregion

    #region Period Lifecycle

    public async Task<FiscalPeriodResult> ClosePeriodAsync(int periodId, int userId, int? storeId = null, CancellationToken ct = default)
    {
        try
        {
            await _unitOfWork.BeginTransactionAsync(ct);

            if (storeId.HasValue)
            {
                // Store-specific close
                var storeStatus = await _context.StoreFiscalPeriods
                    .FirstOrDefaultAsync(sfp =>
                        sfp.FiscalPeriod_ID == periodId &&
                        sfp.Store_ID == storeId.Value, ct);

                if (storeStatus == null)
                {
                    storeStatus = new StoreFiscalPeriod
                    {
                        FiscalPeriod_ID = periodId,
                        Store_ID = storeId.Value,
                        Status = "Closed",
                        ClosedBy = userId,
                        ClosedDate = DateTime.Now
                    };
                    _context.StoreFiscalPeriods.Add(storeStatus);
                }
                else
                {
                    if (storeStatus.Status == "Locked")
                    {
                        return new FiscalPeriodResult
                        {
                            Success = false,
                            ErrorMessage = "Period is locked and cannot be modified"
                        };
                    }
                    storeStatus.Status = "Closed";
                    storeStatus.ClosedBy = userId;
                    storeStatus.ClosedDate = DateTime.Now;
                }
            }
            else
            {
                // Global close
                var period = await GetPeriodByIdAsync(periodId, ct);
                if (period == null)
                {
                    return new FiscalPeriodResult
                    {
                        Success = false,
                        ErrorMessage = $"Period {periodId} not found"
                    };
                }

                if (period.Status == "Locked")
                {
                    return new FiscalPeriodResult
                    {
                        Success = false,
                        ErrorMessage = "Period is locked and cannot be modified"
                    };
                }

                period.Status = "Closed";
                period.ClosedBy = userId;
                period.ClosedDate = DateTime.Now;
            }

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            var updatedPeriod = await GetPeriodByIdAsync(periodId, ct);
            return new FiscalPeriodResult
            {
                Success = true,
                Period = updatedPeriod
            };
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            return new FiscalPeriodResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<FiscalPeriodResult> ReopenPeriodAsync(int periodId, int userId, string reason, int? storeId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return new FiscalPeriodResult
            {
                Success = false,
                ErrorMessage = "Reason is required to reopen a period"
            };
        }

        try
        {
            await _unitOfWork.BeginTransactionAsync(ct);

            if (storeId.HasValue)
            {
                // Store-specific reopen
                var storeStatus = await _context.StoreFiscalPeriods
                    .FirstOrDefaultAsync(sfp =>
                        sfp.FiscalPeriod_ID == periodId &&
                        sfp.Store_ID == storeId.Value, ct);

                if (storeStatus != null)
                {
                    if (storeStatus.Status == "Locked")
                    {
                        return new FiscalPeriodResult
                        {
                            Success = false,
                            ErrorMessage = "Locked periods cannot be reopened"
                        };
                    }
                    storeStatus.Status = "Open";
                    storeStatus.ClosedBy = null;
                    storeStatus.ClosedDate = null;
                }
            }
            else
            {
                // Global reopen
                var period = await GetPeriodByIdAsync(periodId, ct);
                if (period == null)
                {
                    return new FiscalPeriodResult
                    {
                        Success = false,
                        ErrorMessage = $"Period {periodId} not found"
                    };
                }

                if (period.Status == "Locked")
                {
                    return new FiscalPeriodResult
                    {
                        Success = false,
                        ErrorMessage = "Locked periods cannot be reopened"
                    };
                }

                period.Status = "Open";
                period.ClosedBy = null;
                period.ClosedDate = null;
            }

            // Audit note: Period reopened by user {userId} with reason: {reason}

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            var updatedPeriod = await GetPeriodByIdAsync(periodId, ct);
            return new FiscalPeriodResult
            {
                Success = true,
                Period = updatedPeriod
            };
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            return new FiscalPeriodResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<FiscalPeriodResult> LockPeriodAsync(int periodId, int userId, CancellationToken ct = default)
    {
        try
        {
            await _unitOfWork.BeginTransactionAsync(ct);

            var period = await GetPeriodByIdAsync(periodId, ct);
            if (period == null)
            {
                return new FiscalPeriodResult
                {
                    Success = false,
                    ErrorMessage = $"Period {periodId} not found"
                };
            }

            if (period.Status != "Closed")
            {
                return new FiscalPeriodResult
                {
                    Success = false,
                    ErrorMessage = "Period must be closed before it can be locked"
                };
            }

            period.Status = "Locked";
            period.LockedBy = userId;
            period.LockedDate = DateTime.Now;

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            return new FiscalPeriodResult
            {
                Success = true,
                Period = period
            };
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            return new FiscalPeriodResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    #endregion

    #region Fiscal Year Management

    public async Task<FiscalYear> CreateFiscalYearAsync(DateTime startDate, int userId, CancellationToken ct = default)
    {
        await _unitOfWork.BeginTransactionAsync(ct);

        try
        {
            // Calculate year boundaries (fiscal year starts on startDate, ends 12 months later - 1 day)
            var endDate = startDate.AddYears(1).AddDays(-1);
            var yearCode = $"FY-{startDate.Year}";
            var yearName = $"Fiscal Year {startDate.Year}";

            // Check if year already exists
            var existingYear = await _context.FiscalYears
                .FirstOrDefaultAsync(y => y.YearCode == yearCode, ct);

            if (existingYear != null)
            {
                throw new InvalidOperationException($"Fiscal year {yearCode} already exists");
            }

            var fiscalYear = new FiscalYear
            {
                YearCode = yearCode,
                YearName = yearName,
                StartDate = startDate,
                EndDate = endDate,
                Status = "Open",
                CreatedBy = userId,
                CreatedDate = DateTime.Now
            };

            _context.FiscalYears.Add(fiscalYear);
            await _unitOfWork.SaveChangesAsync(ct);

            // Create 12 monthly periods
            var periodStart = startDate;
            for (int month = 1; month <= 12; month++)
            {
                var periodEnd = periodStart.AddMonths(1).AddDays(-1);
                if (periodEnd > endDate) periodEnd = endDate;

                var period = new FiscalPeriod
                {
                    FiscalYear_ID = fiscalYear.FiscalYearID,
                    PeriodNumber = month,
                    PeriodCode = $"{periodStart:yyyy-MM}",
                    PeriodName = $"{periodStart:MMMM yyyy}",
                    StartDate = periodStart,
                    EndDate = periodEnd,
                    Status = "Open",
                    CreatedBy = userId,
                    CreatedDate = DateTime.Now
                };

                _context.FiscalPeriods.Add(period);
                periodStart = periodStart.AddMonths(1);
            }

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            return fiscalYear;
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<List<FiscalYear>> GetFiscalYearsAsync(CancellationToken ct = default)
    {
        return await _context.FiscalYears
            .Include(y => y.FiscalPeriods)
            .OrderByDescending(y => y.StartDate)
            .ToListAsync(ct);
    }


    public async Task<FiscalYear?> GetCurrentFiscalYearAsync(CancellationToken ct = default)
    {
        var today = DateTime.Today;
        return await _context.FiscalYears
            .FirstOrDefaultAsync(y =>
                today >= y.StartDate.Date && today <= y.EndDate.Date, ct);
    }

    public async Task<FiscalPeriodResult> CloseFiscalYearAsync(int fiscalYearId, int userId, CancellationToken ct = default)
    {
        try
        {
            await _unitOfWork.BeginTransactionAsync(ct);

            var fiscalYear = await _context.FiscalYears
                .Include(y => y.FiscalPeriods)
                .FirstOrDefaultAsync(y => y.FiscalYearID == fiscalYearId, ct);

            if (fiscalYear == null)
            {
                return new FiscalPeriodResult
                {
                    Success = false,
                    ErrorMessage = $"Fiscal year {fiscalYearId} not found"
                };
            }

            if (fiscalYear.Status == "Locked")
            {
                return new FiscalPeriodResult
                {
                    Success = false,
                    ErrorMessage = "Fiscal year is locked and cannot be modified"
                };
            }

            // Close all periods in the year
            foreach (var period in fiscalYear.FiscalPeriods)
            {
                if (period.Status == "Open")
                {
                    period.Status = "Closed";
                    period.ClosedBy = userId;
                    period.ClosedDate = DateTime.Now;
                }
            }

            fiscalYear.Status = "Closed";
            fiscalYear.ClosedBy = userId;
            fiscalYear.ClosedDate = DateTime.Now;

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitAsync(ct);

            return new FiscalPeriodResult
            {
                Success = true
            };
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync(ct);
            return new FiscalPeriodResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    #endregion
}


