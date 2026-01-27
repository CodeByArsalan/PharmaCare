using PharmaCare.Domain.Models.Base;

namespace PharmaCare.Domain.Models.AccountManagement;

/// <summary>
/// Represents a fiscal period (month) for posting control
/// </summary>
public class FiscalPeriod : BaseModel
{
    public int FiscalPeriodID { get; set; }

    /// <summary>
    /// Parent fiscal year
    /// </summary>
    public int FiscalYear_ID { get; set; }

    /// <summary>
    /// Period number within year (1-12 for monthly)
    /// </summary>
    public int PeriodNumber { get; set; }

    /// <summary>
    /// Period code, e.g., "2026-01"
    /// </summary>
    public string PeriodCode { get; set; } = string.Empty;

    /// <summary>
    /// Display name, e.g., "January 2026"
    /// </summary>
    public string PeriodName { get; set; } = string.Empty;

    /// <summary>
    /// Start date of the period (inclusive)
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// End date of the period (inclusive)
    /// </summary>
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Status: Open, Closed, Locked
    /// - Open: Normal posting allowed
    /// - Closed: No new entries, adjustments by admin only
    /// - Locked: No changes at all
    /// </summary>
    public string Status { get; set; } = "Open";

    /// <summary>
    /// User who closed the period
    /// </summary>
    public int? ClosedBy { get; set; }

    /// <summary>
    /// Date when period was closed
    /// </summary>
    public DateTime? ClosedDate { get; set; }

    /// <summary>
    /// User who locked the period
    /// </summary>
    public int? LockedBy { get; set; }

    /// <summary>
    /// Date when period was locked
    /// </summary>
    public DateTime? LockedDate { get; set; }

    // Navigation
    public virtual FiscalYear? FiscalYear { get; set; }
    public virtual ICollection<StoreFiscalPeriod> StoreFiscalPeriods { get; set; } = new List<StoreFiscalPeriod>();
}
