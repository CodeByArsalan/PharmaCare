namespace PharmaCare.Infrastructure.Exceptions;

/// <summary>
/// Base exception for journal posting errors.
/// These exceptions MUST bubble up - no catch-and-ignore allowed!
/// </summary>
public class JournalPostingException : Exception
{
    public JournalPostingException(string message) : base(message) { }
    public JournalPostingException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when total debits do not equal total credits.
/// </summary>
public class DebitCreditMismatchException : JournalPostingException
{
    public decimal TotalDebit { get; }
    public decimal TotalCredit { get; }

    public DebitCreditMismatchException(decimal totalDebit, decimal totalCredit)
        : base($"Debit total ({totalDebit:N2}) must equal credit total ({totalCredit:N2}). Difference: {Math.Abs(totalDebit - totalCredit):N2}")
    {
        TotalDebit = totalDebit;
        TotalCredit = totalCredit;
    }

    public DebitCreditMismatchException()
        : base("Debit total must equal credit total.") { }
}

/// <summary>
/// Thrown when attempting to post to a closed or locked fiscal period.
/// </summary>
public class FiscalPeriodClosedException : JournalPostingException
{
    public int? FiscalPeriodId { get; }
    public string? PeriodCode { get; }
    public string? Status { get; }

    public FiscalPeriodClosedException(int fiscalPeriodId, string periodCode, string status)
        : base($"Posting is not allowed in fiscal period '{periodCode}' (Status: {status}).")
    {
        FiscalPeriodId = fiscalPeriodId;
        PeriodCode = periodCode;
        Status = status;
    }

    public FiscalPeriodClosedException(string message)
        : base(message) { }

    public FiscalPeriodClosedException()
        : base("Posting is not allowed in a closed or locked fiscal period.") { }
}

/// <summary>
/// Thrown when attempting to post to an inactive account.
/// </summary>
public class InactiveAccountException : JournalPostingException
{
    public int AccountID { get; }
    public string AccountCode { get; }

    public InactiveAccountException(int accountId, string accountCode)
        : base($"Account '{accountCode}' (ID: {accountId}) is inactive.")
    {
        AccountID = accountId;
        AccountCode = accountCode;
    }

    public InactiveAccountException(string accountCode)
        : base($"Account '{accountCode}' is inactive.")
    {
        AccountCode = accountCode;
    }
}

/// <summary>
/// Thrown when attempting to post a journal that is not in Draft status.
/// </summary>
public class InvalidJournalStatusException : JournalPostingException
{
    public InvalidJournalStatusException(string currentStatus, string requiredStatus)
        : base($"Journal is in '{currentStatus}' status but must be '{requiredStatus}'.") { }
}

/// <summary>
/// Thrown when journal entry has no lines.
/// </summary>
public class EmptyJournalException : JournalPostingException
{
    public EmptyJournalException()
        : base("Journal entry must have at least one line.") { }
}

/// <summary>
/// Thrown when attempting to void a system-generated entry.
/// </summary>
public class SystemEntryVoidException : JournalPostingException
{
    public SystemEntryVoidException()
        : base("System-generated entries cannot be manually voided. Void the source transaction instead.") { }
}
