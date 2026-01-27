using PharmaCare.Domain.Models.AccountManagement;

namespace PharmaCare.Infrastructure.Interfaces.Accounting;

/// <summary>
/// Service for managing fiscal periods and controlling accounting period access.
/// 
/// FISCAL PERIOD LIFECYCLE:
/// Open → Closed → Locked
/// 
/// - Open: Normal posting allowed
/// - Closed: No new entries, adjustments by admin only (with PeriodOverride audit)
/// - Locked: No changes at all (permanent, auditors only)
/// </summary>
public interface IFiscalPeriodService
{
    #region Period Queries

    /// <summary>
    /// Gets the fiscal period for the current date
    /// </summary>
    Task<FiscalPeriod?> GetCurrentPeriodAsync(int? storeId = null, CancellationToken ct = default);

    /// <summary>
    /// Gets the fiscal period for a specific date
    /// </summary>
    Task<FiscalPeriod?> GetPeriodForDateAsync(DateTime date, int? storeId = null, CancellationToken ct = default);

    /// <summary>
    /// Gets all fiscal periods for a fiscal year
    /// </summary>
    Task<List<FiscalPeriod>> GetPeriodsForYearAsync(int fiscalYearId, CancellationToken ct = default);

    /// <summary>
    /// Gets a specific fiscal period by ID
    /// </summary>
    Task<FiscalPeriod?> GetPeriodByIdAsync(int periodId, CancellationToken ct = default);

    /// <summary>
    /// Gets the effective status for a period (considers store-specific overrides)
    /// </summary>
    Task<string> GetEffectiveStatusAsync(int periodId, int? storeId = null, CancellationToken ct = default);

    #endregion

    #region Period Validation

    /// <summary>
    /// Validates that a period is open for posting. Throws FiscalPeriodClosedException if not.
    /// </summary>
    Task ValidatePeriodOpenAsync(DateTime postingDate, int? storeId = null, CancellationToken ct = default);

    /// <summary>
    /// Checks if a period is open for posting (does not throw)
    /// </summary>
    Task<bool> IsPeriodOpenAsync(DateTime postingDate, int? storeId = null, CancellationToken ct = default);

    #endregion

    #region Period Lifecycle

    /// <summary>
    /// Closes a fiscal period (prevents normal entries, admin adjustments allowed)
    /// </summary>
    Task<FiscalPeriodResult> ClosePeriodAsync(int periodId, int userId, int? storeId = null, CancellationToken ct = default);

    /// <summary>
    /// Reopens a closed period (requires reason for audit)
    /// </summary>
    Task<FiscalPeriodResult> ReopenPeriodAsync(int periodId, int userId, string reason, int? storeId = null, CancellationToken ct = default);

    /// <summary>
    /// Permanently locks a period (no changes allowed ever)
    /// </summary>
    Task<FiscalPeriodResult> LockPeriodAsync(int periodId, int userId, CancellationToken ct = default);

    #endregion

    #region Fiscal Year Management

    /// <summary>
    /// Creates a new fiscal year with 12 monthly periods
    /// </summary>
    Task<FiscalYear> CreateFiscalYearAsync(DateTime startDate, int userId, CancellationToken ct = default);

    /// <summary>
    /// Gets all fiscal years
    /// </summary>
    Task<List<FiscalYear>> GetFiscalYearsAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the current fiscal year (containing today's date)
    /// </summary>
    Task<FiscalYear?> GetCurrentFiscalYearAsync(CancellationToken ct = default);

    /// <summary>
    /// Closes an entire fiscal year (closes all periods)
    /// </summary>
    Task<FiscalPeriodResult> CloseFiscalYearAsync(int fiscalYearId, int userId, CancellationToken ct = default);

    #endregion
}



#region DTOs

/// <summary>
/// Result of a fiscal period operation
/// </summary>
public class FiscalPeriodResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public FiscalPeriod? Period { get; set; }
}

#endregion
