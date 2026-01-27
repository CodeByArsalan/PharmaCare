using PharmaCare.Domain.Models.Configuration;

namespace PharmaCare.Domain.Models.AccountManagement;

/// <summary>
/// Journal Entry Line - individual debit/credit lines within a journal entry
/// </summary>
public class JournalEntryLine
{
    public int JournalEntryLineID { get; set; }

    /// <summary>
    /// Foreign key to parent journal entry
    /// </summary>
    public int JournalEntry_ID { get; set; }

    /// <summary>
    /// Line number for ordering (1, 2, 3, etc.)
    /// </summary>
    public int LineNumber { get; set; }

    /// <summary>
    /// Account affected by this line
    /// </summary>
    public int Account_ID { get; set; }

    /// <summary>
    /// Debit amount (0 if this is a credit line)
    /// </summary>
    public decimal DebitAmount { get; set; }

    /// <summary>
    /// Credit amount (0 if this is a debit line)
    /// </summary>
    public decimal CreditAmount { get; set; }

    /// <summary>
    /// Line-specific description/narrative
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Optional store tracking for multi-location analysis
    /// </summary>
    public int? Store_ID { get; set; }

    // Navigation Properties
    public virtual JournalEntry? JournalEntry { get; set; }
    public virtual ChartOfAccount? Account { get; set; }
    public virtual Store? Store { get; set; }
}
