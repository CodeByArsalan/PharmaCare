using PharmaCare.Domain.Models.Base;

namespace PharmaCare.Domain.Models.AccountManagement;

/// <summary>
/// Represents a fiscal year for accounting period control
/// </summary>
public class FiscalYear : BaseModel
{
    public int FiscalYearID { get; set; }

    /// <summary>
    /// Year code, e.g., "FY-2026"
    /// </summary>
    public string YearCode { get; set; } = string.Empty;

    /// <summary>
    /// Display name, e.g., "Fiscal Year 2026"
    /// </summary>
    public string YearName { get; set; } = string.Empty;

    /// <summary>
    /// Start date of the fiscal year
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// End date of the fiscal year
    /// </summary>
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Status: Open, Closed, Locked
    /// </summary>
    public string Status { get; set; } = "Open";

    /// <summary>
    /// User who closed/locked the year
    /// </summary>
    public int? ClosedBy { get; set; }

    /// <summary>
    /// Date when year was closed
    /// </summary>
    public DateTime? ClosedDate { get; set; }

    // Navigation
    public virtual ICollection<FiscalPeriod> FiscalPeriods { get; set; } = new List<FiscalPeriod>();
}
