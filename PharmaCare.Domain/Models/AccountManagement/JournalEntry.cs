using PharmaCare.Domain.Models.Base;
using PharmaCare.Domain.Models.Inventory;

namespace PharmaCare.Domain.Models.AccountManagement;

/// <summary>
/// Journal Entry - header for each accounting transaction in the double-entry system
/// </summary>
public class JournalEntry : BaseModel
{
    public int JournalEntryID { get; set; }

    /// <summary>
    /// Auto-generated unique entry number (e.g., JE-2024-00001)
    /// </summary>
    public string EntryNumber { get; set; } = string.Empty;

    /// <summary>
    /// Date of the transaction
    /// </summary>
    public DateTime EntryDate { get; set; }

    /// <summary>
    /// Date when the entry is posted (affects account balances)
    /// </summary>
    public DateTime PostingDate { get; set; }

    /// <summary>
    /// Type of entry: Sale, Purchase, Payment, Receipt, Adjustment, Opening, Closing, Reversal
    /// </summary>
    public string EntryType { get; set; } = string.Empty;

    /// <summary>
    /// Reference to external document/transaction
    /// </summary>
    public string? Reference { get; set; }

    /// <summary>
    /// Narrative description of the transaction
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Sum of all debit amounts (must equal TotalCredit)
    /// </summary>
    public decimal TotalDebit { get; set; }

    /// <summary>
    /// Sum of all credit amounts (must equal TotalDebit)
    /// </summary>
    public decimal TotalCredit { get; set; }

    /// <summary>
    /// Status: Draft, Posted, Void
    /// </summary>
    public string Status { get; set; } = "Draft";

    /// <summary>
    /// Source table name for tracking original transaction (e.g., "Sale", "Expense")
    /// </summary>
    public string? Source_Table { get; set; }

    /// <summary>
    /// Source record ID for linking back to original transaction
    /// </summary>
    public int? Source_ID { get; set; }

    /// <summary>
    /// User who posted the entry
    /// </summary>
    public int? PostedBy { get; set; }

    /// <summary>
    /// Date when the entry was posted
    /// </summary>
    public DateTime? PostedDate { get; set; }

    // ========== STORE & PERIOD ==========

    /// <summary>
    /// Store ID at header level for query performance and segregation
    /// </summary>
    public int? Store_ID { get; set; }

    /// <summary>
    /// Fiscal period this entry belongs to (for period control)
    /// </summary>
    public int? FiscalPeriod_ID { get; set; }

    /// <summary>
    /// Indicates this is a system-generated entry (cannot be manually voided)
    /// </summary>
    public bool IsSystemEntry { get; set; } = false;

    // ========== REVERSAL TRACKING ==========

    /// <summary>
    /// ID of the original entry this one reverses (set on reversing entries)
    /// </summary>
    public int? ReversesEntry_ID { get; set; }

    /// <summary>
    /// ID of the reversing entry that voided this one (set on voided entries)
    /// </summary>
    public int? ReversedByEntry_ID { get; set; }

    // ========== NAVIGATION PROPERTIES ==========

    public virtual ICollection<JournalEntryLine> JournalEntryLines { get; set; } = new List<JournalEntryLine>();

    /// <summary>
    /// The original entry that this entry reverses
    /// </summary>
    public virtual JournalEntry? ReversesEntry { get; set; }

    /// <summary>
    /// The reversing entry that voided this entry
    /// </summary>
    public virtual JournalEntry? ReversedByEntry { get; set; }

    /// <summary>
    /// Fiscal period navigation
    /// </summary>
    public virtual FiscalPeriod? FiscalPeriod { get; set; }

    /// <summary>
    /// Stock movements linked to this journal entry
    /// </summary>
    public virtual ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
}

