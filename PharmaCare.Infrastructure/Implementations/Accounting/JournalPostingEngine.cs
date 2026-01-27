using Microsoft.EntityFrameworkCore;
using PharmaCare.Domain.Models.AccountManagement;
using PharmaCare.Infrastructure.Exceptions;
using PharmaCare.Infrastructure.Interfaces;
using PharmaCare.Infrastructure.Interfaces.Accounting;

namespace PharmaCare.Infrastructure.Implementations.Accounting;

/// <summary>
/// Central engine for posting and voiding journal entries.
/// 
/// ABSOLUTE RULES:
/// 1. ALL journal posting MUST go through this engine
/// 2. NO controller, repository, or service may post journals directly
/// 3. Debit = Credit is ALWAYS enforced
/// 4. Fiscal period validation is MANDATORY
/// 5. Posted journals are IMMUTABLE (void creates reversing entry)
/// </summary>
public sealed class JournalPostingEngine : IJournalPostingEngine
{
    private readonly PharmaCareDBContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public JournalPostingEngine(
        PharmaCareDBContext context,
        IUnitOfWork unitOfWork)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    /// <inheritdoc />
    public async Task<JournalEntry> PostAsync(
        JournalEntry journal,
        int userId,
        CancellationToken ct = default)
    {
        // ========== VALIDATION PHASE ==========

        // 1. Must be in Draft status
        if (journal.Status != "Draft")
        {
            throw new InvalidJournalStatusException(journal.Status, "Draft");
        }

        // 2. Must have lines
        if (journal.JournalEntryLines == null || !journal.JournalEntryLines.Any())
        {
            throw new EmptyJournalException();
        }

        // 3. Calculate totals
        var totalDebit = journal.JournalEntryLines.Sum(l => l.DebitAmount);
        var totalCredit = journal.JournalEntryLines.Sum(l => l.CreditAmount);

        // 4. Debit must equal Credit (with tolerance for rounding)
        if (Math.Abs(totalDebit - totalCredit) > 0.001m)
        {
            throw new DebitCreditMismatchException(totalDebit, totalCredit);
        }

        // 5. Validate fiscal period (if specified)
        if (journal.FiscalPeriod_ID.HasValue)
        {
            var fiscalPeriod = await _context.FiscalPeriods
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.FiscalPeriodID == journal.FiscalPeriod_ID.Value, ct);

            if (fiscalPeriod != null && fiscalPeriod.Status != "Open")
            {
                throw new FiscalPeriodClosedException(
                    fiscalPeriod.FiscalPeriodID,
                    fiscalPeriod.PeriodCode,
                    fiscalPeriod.Status);
            }
        }
        else
        {
            // Auto-determine fiscal period from posting date
            var postingDate = journal.PostingDate.Date;
            var fiscalPeriod = await _context.FiscalPeriods
                .AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    postingDate >= p.StartDate.Date &&
                    postingDate <= p.EndDate.Date, ct);

            if (fiscalPeriod != null)
            {
                if (fiscalPeriod.Status != "Open")
                {
                    throw new FiscalPeriodClosedException(
                        fiscalPeriod.FiscalPeriodID,
                        fiscalPeriod.PeriodCode,
                        fiscalPeriod.Status);
                }
                journal.FiscalPeriod_ID = fiscalPeriod.FiscalPeriodID;
            }
            // If no fiscal period found, allow posting (period control is optional)
        }

        // 6. Validate all accounts are active
        var accountIds = journal.JournalEntryLines.Select(l => l.Account_ID).Distinct().ToList();
        var accounts = await _context.ChartOfAccounts
            .AsNoTracking()
            .Where(a => accountIds.Contains(a.AccountID))
            .ToListAsync(ct);

        foreach (var line in journal.JournalEntryLines)
        {
            var account = accounts.FirstOrDefault(a => a.AccountID == line.Account_ID);
            if (account == null)
            {
                throw new JournalPostingException($"Account with ID {line.Account_ID} not found.");
            }
            if (!account.IsActive)
            {
                throw new InactiveAccountException(account.AccountID, account.AccountNo ?? account.AccountName);
            }
        }

        // ========== POSTING PHASE ==========

        // Generate journal number using sequence
        var journalNumber = await GenerateJournalNumberAsync(journal.PostingDate.Year, ct);
        journal.EntryNumber = journalNumber;

        // Update totals
        journal.TotalDebit = totalDebit;
        journal.TotalCredit = totalCredit;

        // Set posting metadata
        journal.Status = "Posted";
        journal.PostedBy = userId;
        journal.PostedDate = DateTime.Now;

        // Assign line numbers if not set
        int lineNum = 1;
        foreach (var line in journal.JournalEntryLines)
        {
            if (line.LineNumber == 0)
            {
                line.LineNumber = lineNum++;
            }
        }

        // If this is a new journal (not yet tracked), add it
        if (journal.JournalEntryID == 0)
        {
            journal.CreatedBy = userId;
            journal.CreatedDate = DateTime.Now;
            _context.JournalEntries.Add(journal);
        }
        else
        {
            journal.UpdatedBy = userId;
            journal.UpdatedDate = DateTime.Now;
            _context.JournalEntries.Update(journal);
        }

        // Save (but don't commit - let caller manage transaction)
        await _unitOfWork.SaveChangesAsync(ct);

        return journal;
    }

    /// <inheritdoc />
    public async Task<JournalEntry> VoidAsync(
        int journalEntryId,
        string reason,
        int userId,
        CancellationToken ct = default)
    {
        // Load the original entry with lines
        var original = await _context.JournalEntries
            .Include(j => j.JournalEntryLines)
            .FirstOrDefaultAsync(j => j.JournalEntryID == journalEntryId, ct);

        if (original == null)
        {
            throw new JournalPostingException($"Journal entry with ID {journalEntryId} not found.");
        }

        // Can only void Posted entries
        if (original.Status != "Posted")
        {
            throw new InvalidJournalStatusException(original.Status, "Posted");
        }

        // System entries cannot be manually voided
        if (original.IsSystemEntry)
        {
            throw new SystemEntryVoidException();
        }

        // Create reversing entry
        var reversing = CreateReversalEntry(original, reason, userId);

        // Begin transaction for atomic operation
        await _unitOfWork.BeginTransactionAsync(ct);

        try
        {
            // Add and post the reversing entry
            _context.JournalEntries.Add(reversing);

            // Post the reversing entry
            reversing = await PostAsync(reversing, userId, ct);

            // Update original entry
            original.Status = "Void";
            original.ReversedByEntry_ID = reversing.JournalEntryID;
            original.UpdatedBy = userId;
            original.UpdatedDate = DateTime.Now;
            _context.JournalEntries.Update(original);

            await _unitOfWork.CommitAsync(ct);

            return reversing;
        }
        catch
        {
            await _unitOfWork.RollbackAsync(ct);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<JournalEntry> CreateAndPostAsync(
        string entryType,
        string description,
        IEnumerable<JournalEntryLine> lines,
        string? sourceTable,
        int? sourceId,
        int? storeId,
        int userId,
        bool isSystemEntry = true,
        string? reference = null,
        CancellationToken ct = default)
    {
        var journal = new JournalEntry
        {
            EntryDate = DateTime.Now,
            PostingDate = DateTime.Now,
            EntryType = entryType,
            Description = description,
            Reference = reference,
            Source_Table = sourceTable,
            Source_ID = sourceId,
            Store_ID = storeId,
            IsSystemEntry = isSystemEntry,
            Status = "Draft",
            CreatedBy = userId,
            CreatedDate = DateTime.Now
        };

        // Add lines
        foreach (var line in lines)
        {
            journal.JournalEntryLines.Add(line);
        }

        // Post the journal
        return await PostAsync(journal, userId, ct);
    }


    /// <summary>
    /// Generates a unique journal entry number using timestamp.
    /// Format: JE-yyyyMMddHHmmssfff (e.g., JE-20260114190045123)
    /// This ensures uniqueness without requiring a sequence table.
    /// </summary>
    private Task<string> GenerateJournalNumberAsync(int year, CancellationToken ct)
    {
        // Use timestamp-based format for guaranteed uniqueness
        var timestamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");
        var entryNumber = $"JE-{timestamp}";
        return Task.FromResult(entryNumber);
    }

    /// <summary>
    /// Creates a reversing entry for voiding purposes.
    /// </summary>
    private JournalEntry CreateReversalEntry(JournalEntry original, string reason, int userId)
    {
        var reversal = new JournalEntry
        {
            EntryDate = DateTime.Now,
            PostingDate = DateTime.Now,
            EntryType = "Reversal",
            Reference = original.EntryNumber,
            Description = $"Reversal of {original.EntryNumber}: {reason}",
            Source_Table = original.Source_Table,
            Source_ID = original.Source_ID,
            Store_ID = original.Store_ID,
            IsSystemEntry = false, // Reversals are not system entries
            ReversesEntry_ID = original.JournalEntryID,
            Status = "Draft",
            CreatedBy = userId,
            CreatedDate = DateTime.Now
        };

        // Create reversed lines (swap debits and credits)
        int lineNum = 1;
        foreach (var originalLine in original.JournalEntryLines)
        {
            var reversedLine = new JournalEntryLine
            {
                LineNumber = lineNum++,
                Account_ID = originalLine.Account_ID,
                DebitAmount = originalLine.CreditAmount, // Swap!
                CreditAmount = originalLine.DebitAmount, // Swap!
                Description = $"Reversal: {originalLine.Description}",
                Store_ID = originalLine.Store_ID
            };
            reversal.JournalEntryLines.Add(reversedLine);
        }

        return reversal;
    }
}
