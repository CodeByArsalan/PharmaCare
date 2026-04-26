using PharmaCare.Domain.Entities.Accounting;

namespace PharmaCare.Application.Interfaces.Accounting;

public interface IFinancialPeriodService
{
    /// <summary>
    /// Checks if the given date falls within a closed/locked financial period.
    /// </summary>
    Task<bool> IsPeriodLockedAsync(DateTime date);

    /// <summary>
    /// Gets all financial periods.
    /// </summary>
    Task<IEnumerable<FinancialPeriod>> GetAllAsync();

    /// <summary>
    /// Gets a period by ID.
    /// </summary>
    Task<FinancialPeriod?> GetByIdAsync(int id);

    /// <summary>
    /// Creates a new financial period.
    /// </summary>
    Task<FinancialPeriod> CreateAsync(FinancialPeriod period, int userId);

    /// <summary>
    /// Closes/Locks a financial period.
    /// </summary>
    Task<bool> ClosePeriodAsync(int periodId, string? remarks, int userId);

    /// <summary>
    /// Re-opens a closed financial period.
    /// </summary>
    Task<bool> OpenPeriodAsync(int periodId, int userId);
}
