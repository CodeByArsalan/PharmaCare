using PharmaCare.Domain.Models.Configuration;

namespace PharmaCare.Domain.Models.AccountManagement;

/// <summary>
/// Store-specific fiscal period status (allows different stores to close at different times)
/// </summary>
public class StoreFiscalPeriod
{
    public int StoreFiscalPeriodID { get; set; }

    /// <summary>
    /// Store this override applies to
    /// </summary>
    public int Store_ID { get; set; }

    /// <summary>
    /// Fiscal period this override applies to
    /// </summary>
    public int FiscalPeriod_ID { get; set; }

    /// <summary>
    /// Store-specific status: Open, Closed, Locked (overrides parent period if set)
    /// </summary>
    public string Status { get; set; } = "Open";

    /// <summary>
    /// User who closed this store's period
    /// </summary>
    public int? ClosedBy { get; set; }

    /// <summary>
    /// Date when this store's period was closed
    /// </summary>
    public DateTime? ClosedDate { get; set; }

    // Navigation
    public virtual Store? Store { get; set; }
    public virtual FiscalPeriod? FiscalPeriod { get; set; }
}
