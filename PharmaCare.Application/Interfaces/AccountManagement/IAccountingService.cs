using PharmaCare.Application.DTOs.Accounting;
using PharmaCare.Domain.Models.AccountManagement;

namespace PharmaCare.Application.Interfaces.AccountManagement;

public interface IAccountingService
{
    // ==================== CHART OF ACCOUNTS ====================

    Task<List<AccountType>> GetAccountTypes();

    /// <summary>
    /// Get accounts by account type ID
    /// </summary>
    Task<List<ChartOfAccount>> GetAccountsByType(int accountTypeId);

    /// <summary>
    /// Get all chart of accounts (optionally active only)
    /// </summary>
    Task<List<ChartOfAccountDto>> GetChartOfAccounts(bool activeOnly = true);

    /// <summary>
    /// Get chart of accounts in hierarchical structure
    /// </summary>
    Task<List<ChartOfAccountDto>> GetChartOfAccountsHierarchy(bool activeOnly = true);

    /// <summary>
    /// Get account by ID
    /// </summary>
    Task<ChartOfAccount?> GetAccountById(int accountId);

    /// <summary>
    /// Get account by code
    /// </summary>
    Task<ChartOfAccount?> GetAccountByCode(string accountCode);

    /// <summary>
    /// Create new account
    /// </summary>
    Task<bool> CreateAccount(ChartOfAccount account, int userId);

    /// <summary>
    /// Ensures a sub-account exists. If it doesn't, it creates it.
    /// </summary>
    /// <param name="code">The code of the sub-account.</param>
    /// <param name="name">The name of the sub-account.</param>
    /// <param name="parentCode">The code of the parent account.</param>
    /// <param name="userId">The ID of the user performing the action.</param>
    /// <returns>The ID of the existing or newly created sub-account.</returns>
    Task<int> EnsureSubAccountExists(string code, string name, string parentCode, int userId);

    /// <summary>
    /// Update existing account
    /// </summary>
    Task<bool> UpdateAccount(ChartOfAccount account, int userId);

    /// <summary>
    /// Deactivate account (soft delete)
    /// </summary>
    Task<bool> DeactivateAccount(int accountId, int userId);

    /// <summary>
    /// Get first active account by AccountType_ID
    /// </summary>
    Task<ChartOfAccount?> GetFirstAccountByTypeId(int accountTypeId);


    // ==================== JOURNAL ENTRIES ====================

    /// <summary>
    /// Create a new journal entry (in Draft status)
    /// </summary>
    Task<int> CreateJournalEntry(JournalEntryDto dto, int userId);

    /// <summary>
    /// Post a journal entry (validates and changes status to Posted)
    /// </summary>
    Task<bool> PostJournalEntry(int journalEntryId, int userId);

    /// <summary>
    /// Void a journal entry
    /// </summary>
    Task<bool> VoidJournalEntry(int journalEntryId, int userId);

    /// <summary>
    /// Get journal entry by ID
    /// </summary>
    Task<JournalEntryDto?> GetJournalEntryById(int journalEntryId);

    /// <summary>
    /// Get journal entries with filters
    /// </summary>
    Task<List<JournalEntryDto>> GetJournalEntries(DateTime? fromDate = null, DateTime? toDate = null, string? status = null, string? entryType = null);

    /// <summary>
    /// Generate next journal entry number
    /// </summary>
    Task<string> GenerateJournalEntryNumber();

    // ==================== BALANCE & REPORTING ====================

    /// <summary>
    /// Get account balance as of a specific date
    /// </summary>
    Task<decimal> GetAccountBalance(int accountId, DateTime? asOfDate = null);

    /// <summary>
    /// Get account balance by account code
    /// </summary>
    Task<decimal> GetAccountBalanceByCode(string accountCode, DateTime? asOfDate = null);

    /// <summary>
    /// Generate trial balance report
    /// </summary>
    /// <param name="asOfDate">Date as of which to generate the report</param>
    /// <param name="storeId">Optional store ID for store-specific report</param>
    Task<TrialBalanceDto> GetTrialBalance(DateTime asOfDate, int? storeId = null);

    /// <summary>
    /// Generate balance sheet
    /// </summary>
    /// <param name="asOfDate">Date as of which to generate the report</param>
    /// <param name="storeId">Optional store ID for store-specific report</param>
    Task<BalanceSheetDto> GetBalanceSheet(DateTime asOfDate, int? storeId = null);

    /// <summary>
    /// Generate income statement
    /// </summary>
    /// <param name="fromDate">Start date of the period</param>
    /// <param name="toDate">End date of the period</param>
    /// <param name="storeId">Optional store ID for store-specific report</param>
    Task<IncomeStatementDto> GetIncomeStatement(DateTime fromDate, DateTime toDate, int? storeId = null);

    /// <summary>
    /// Generate general ledger for a specific account
    /// </summary>
    /// <param name="accountId">Account ID to generate ledger for</param>
    /// <param name="fromDate">Optional start date filter</param>
    /// <param name="toDate">Optional end date filter</param>
    /// <param name="storeId">Optional store ID for store-specific report</param>
    Task<GeneralLedgerDto> GetGeneralLedger(int accountId, DateTime? fromDate = null, DateTime? toDate = null, int? storeId = null);

    // ==================== VALIDATION ====================

    /// <summary>
    /// Validate journal entry (checks if debits = credits, all accounts exist, etc.)
    /// </summary>
    Task<(bool IsValid, List<string> Errors)> ValidateJournalEntry(JournalEntryDto dto);
}
