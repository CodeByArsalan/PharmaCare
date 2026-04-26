using System.ComponentModel.DataAnnotations;
using PharmaCare.Domain.Entities.Base;

namespace PharmaCare.Domain.Entities.Accounting;

/// <summary>
/// Represents a financial period (e.g., a month) that can be locked to prevent back-dated transactions.
/// </summary>
public class FinancialPeriod : BaseEntity
{
    [Key]
    public int PeriodID { get; set; }

    [Required]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    /// <summary>
    /// If true, no transactions can be recorded within this date range.
    /// </summary>
    public bool IsClosed { get; set; }

    public DateTime? ClosedAt { get; set; }
    public int? ClosedBy { get; set; }
    
    [StringLength(500)]
    public string? Remarks { get; set; }
}
