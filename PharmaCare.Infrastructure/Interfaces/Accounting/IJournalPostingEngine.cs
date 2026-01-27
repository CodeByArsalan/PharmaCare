using PharmaCare.Domain.Models.AccountManagement;

namespace PharmaCare.Infrastructure.Interfaces.Accounting;

/// <summary>
/// Central engine for posting and voiding journal entries.
/// 
/// CRITICAL RULES:
/// 1. ALL journal posting MUST go through this engine
/// 2. NO controller, repository, or service may post journals directly
/// 3. Debit = Credit is ALWAYS enforced
/// 4. Fiscal period validation is MANDATORY
/// 5. Posted journals are IMMUTABLE (void creates reversing entry)
/// </summary>
public interface IJournalPostingEngine
{
    /// <summary>
    /// Posts a draft journal entry after validation.
    /// </summary>
    Task<JournalEntry> PostAsync(JournalEntry journalEntry, int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Voids a posted journal entry by creating a reversing entry.
    /// </summary>
    Task<JournalEntry> VoidAsync(int journalEntryId, string reason, int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates and posts a journal entry in one atomic operation.
    /// Use this for system-generated entries (sales, purchases, etc.)
    /// </summary>
    Task<JournalEntry> CreateAndPostAsync(
        string entryType,
        string description,
        IEnumerable<JournalEntryLine> lines,
        string? sourceTable,
        int? sourceId,
        int? storeId,
        int userId,
        bool isSystemEntry = true,
        string? reference = null,
        CancellationToken cancellationToken = default);
}

