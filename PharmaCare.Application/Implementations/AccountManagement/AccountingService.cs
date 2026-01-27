using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.DTOs.Accounting;
using PharmaCare.Application.Interfaces.AccountManagement;
using PharmaCare.Domain.Models.AccountManagement;
using PharmaCare.Application.Utilities;
using PharmaCare.Infrastructure.Interfaces;
using PharmaCare.Infrastructure.Interfaces.Accounting;

namespace PharmaCare.Application.Implementations.AccountManagement;

public class AccountingService : IAccountingService
{
    private readonly IRepository<AccountType> _accountTypeRepo;
    private readonly IRepository<ChartOfAccount> _chartOfAccountRepo;
    private readonly IRepository<JournalEntry> _journalEntryRepo;
    private readonly IRepository<JournalEntryLine> _journalEntryLineRepo;
    private readonly IRepository<Head> _headRepo;
    private readonly IJournalPostingEngine _postingEngine;

    public AccountingService(
        IRepository<AccountType> accountTypeRepo,
        IRepository<ChartOfAccount> chartOfAccountRepo,
        IRepository<JournalEntry> journalEntryRepo,
        IRepository<JournalEntryLine> journalEntryLineRepo,
        IRepository<Head> headRepo,
        IJournalPostingEngine postingEngine)
    {
        _accountTypeRepo = accountTypeRepo;
        _chartOfAccountRepo = chartOfAccountRepo;
        _journalEntryRepo = journalEntryRepo;
        _journalEntryLineRepo = journalEntryLineRepo;
        _headRepo = headRepo;
        _postingEngine = postingEngine;
    }

    // ==================== CHART OF ACCOUNTS ====================

    public async Task<List<AccountType>> GetAccountTypes()
    {
        return (await _accountTypeRepo.GetAll()).ToList();
    }

    public async Task<List<ChartOfAccount>> GetAccountsByType(int accountTypeId)
    {
        return await _chartOfAccountRepo.FindByCondition(a => a.AccountType_ID == accountTypeId && a.IsActive)
            .OrderBy(a => a.AccountName)
            .ToListAsync();
    }

    public async Task<List<ChartOfAccountDto>> GetChartOfAccounts(bool activeOnly = true)
    {
        var query = _chartOfAccountRepo.GetAllWithInclude(coa => coa.Head, coa => coa.Subhead, coa => coa.AccountType);

        if (activeOnly)
            query = query.Where(coa => coa.IsActive);

        var accounts = await query.OrderBy(coa => coa.Head!.Family).ThenBy(coa => coa.AccountName).ToListAsync();

        return accounts.Select(MapToDto).ToList();
    }

    public async Task<List<ChartOfAccountDto>> GetChartOfAccountsHierarchy(bool activeOnly = true)
    {
        return await GetChartOfAccounts(activeOnly);
    }

    public async Task<ChartOfAccount?> GetAccountById(int accountId)
    {
        return await _chartOfAccountRepo.FindByCondition(a => a.AccountID == accountId)
            .Include(a => a.Head)
            .Include(a => a.Subhead)
            .Include(a => a.AccountType)
            .FirstOrDefaultAsync();
    }

    public async Task<ChartOfAccount?> GetAccountByCode(string accountCode)
    {
        // This method is no longer applicable as AccountCode was removed
        return null;
    }

    public async Task<ChartOfAccount?> GetAccountByType(string accountType)
    {
        return await _chartOfAccountRepo.FindByCondition(a => a.AccountType != null && a.AccountType.TypeName == accountType && a.IsActive)
            .Include(a => a.Head)
            .Include(a => a.Subhead)
            .Include(a => a.AccountType)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> CreateAccount(ChartOfAccount account, int userId)
    {
        account.IsActive = true;
        account.CreatedBy = userId;
        account.CreatedDate = DateTime.Now;

        return await _chartOfAccountRepo.Insert(account);
    }

    public async Task<int> EnsureSubAccountExists(string code, string name, string parentCode, int userId)
    {
        // This method is deprecated since we no longer use AccountCode
        // Return existing account by name or create new one
        var existing = await _chartOfAccountRepo.FindByCondition(a => a.AccountName == name && a.IsActive).FirstOrDefaultAsync();
        return existing?.AccountID ?? 0;
    }

    public async Task<bool> UpdateAccount(ChartOfAccount account, int userId)
    {
        var existing = _chartOfAccountRepo.GetById(account.AccountID);
        if (existing == null) return false;

        existing.AccountName = account.AccountName;
        existing.AccountType_ID = account.AccountType_ID;
        existing.AccountNo = account.AccountNo;
        existing.IBAN = account.IBAN;
        existing.AccountAddress = account.AccountAddress;
        existing.Head_ID = account.Head_ID;
        existing.Subhead_ID = account.Subhead_ID;
        existing.UpdatedBy = userId;
        existing.UpdatedDate = DateTime.Now;

        return await _chartOfAccountRepo.Update(existing);
    }

    public async Task<bool> DeactivateAccount(int accountId, int userId)
    {
        var account = _chartOfAccountRepo.GetById(accountId);
        if (account == null) return false;

        // Check if account has posted transactions
        var hasTransactions = await _journalEntryLineRepo
            .FindByCondition(jel => jel.Account_ID == accountId)
            .AnyAsync();

        if (hasTransactions)
            return false; // Cannot deactivate account with transactions

        account.IsActive = false;
        account.UpdatedBy = userId;
        account.UpdatedDate = DateTime.Now;

        return await _chartOfAccountRepo.Update(account);
    }

    public async Task<ChartOfAccount?> GetFirstAccountByTypeId(int accountTypeId)
    {
        return await _chartOfAccountRepo
            .FindByCondition(a => a.AccountType_ID == accountTypeId && a.IsActive)
            .Include(a => a.Head)
            .Include(a => a.Subhead)
            .FirstOrDefaultAsync();
    }

    // ==================== JOURNAL ENTRIES ====================

    /// <summary>
    /// Get account by AccountNo (used as AccountCode for compatibility)
    /// </summary>
    public async Task<ChartOfAccount?> GetAccountByAccountNo(string accountNo)
    {
        return await _chartOfAccountRepo.FindByCondition(a => a.AccountNo == accountNo && a.IsActive)
            .Include(a => a.Head)
            .Include(a => a.Subhead)
            .FirstOrDefaultAsync();
    }

    public async Task<int> CreateJournalEntry(JournalEntryDto dto, int userId)
    {
        // First, resolve AccountCode to Account_ID for each line BEFORE validation
        if (dto.Lines != null)
        {
            foreach (var lineDto in dto.Lines)
            {
                if ((lineDto.Account_ID == null || lineDto.Account_ID == 0) && !string.IsNullOrEmpty(lineDto.AccountCode))
                {
                    var account = await GetAccountByAccountNo(lineDto.AccountCode);
                    if (account != null)
                        lineDto.Account_ID = account.AccountID;
                }
            }
        }

        // Now validate with resolved Account_IDs
        var validation = await ValidateJournalEntry(dto);
        if (!validation.IsValid)
            throw new InvalidOperationException($"Journal entry validation failed: {string.Join(", ", validation.Errors)}");

        // Generate entry number if not provided
        if (string.IsNullOrEmpty(dto.EntryNumber))
            dto.EntryNumber = await GenerateJournalEntryNumber();

        // Create journal entry
        // Create journal entry
        var journalEntry = new JournalEntry
        {
            EntryNumber = dto.EntryNumber,
            EntryDate = dto.EntryDate == default ? DateTime.Now : dto.EntryDate,
            PostingDate = dto.PostingDate == default ? DateTime.Now : dto.PostingDate,
            EntryType = string.IsNullOrEmpty(dto.EntryType) ? "Manual" : dto.EntryType,
            Reference = dto.Reference,
            Description = dto.Description,
            TotalDebit = dto.Lines.Sum(l => l.DebitAmount),
            TotalCredit = dto.Lines.Sum(l => l.CreditAmount),
            Status = "Draft",
            Source_Table = dto.Source_Table,
            Source_ID = dto.Source_ID,
            CreatedBy = userId,
            CreatedDate = DateTime.Now
        };

        if (!await _journalEntryRepo.Insert(journalEntry))
            return 0;

        // Create lines (Account_ID already resolved)
        int lineNumber = 1;
        foreach (var lineDto in dto.Lines)
        {
            var line = new JournalEntryLine
            {
                JournalEntry_ID = journalEntry.JournalEntryID,
                LineNumber = lineNumber++,
                Account_ID = lineDto.Account_ID ?? 0,
                DebitAmount = lineDto.DebitAmount,
                CreditAmount = lineDto.CreditAmount,
                Description = lineDto.Description,
                Store_ID = lineDto.Store_ID
            };

            await _journalEntryLineRepo.Insert(line);
        }

        return journalEntry.JournalEntryID;
    }

    public async Task<bool> PostJournalEntry(int journalEntryId, int userId)
    {
        var entry = await _journalEntryRepo.FindByCondition(je => je.JournalEntryID == journalEntryId)
            .Include(je => je.JournalEntryLines)
            .FirstOrDefaultAsync();

        if (entry == null || entry.Status != "Draft")
            return false;

        // Validate one more time before posting
        var dto = await GetJournalEntryById(journalEntryId);
        if (dto == null) return false;

        var validation = await ValidateJournalEntry(dto);
        if (!validation.IsValid)
            return false;

        try
        {
            await _postingEngine.PostAsync(entry, userId);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> VoidJournalEntry(int journalEntryId, int userId)
    {
        // Get the original entry with its lines
        var originalEntry = await _journalEntryRepo.FindByCondition(je => je.JournalEntryID == journalEntryId)
            .Include(je => je.JournalEntryLines)
            .FirstOrDefaultAsync();

        // Cannot void: null, already void, or reversal entries
        if (originalEntry == null || originalEntry.Status == "Void" || originalEntry.ReversesEntry_ID.HasValue)
            return false;

        // Generate a new entry number for the reversing entry
        var reversingEntryNumber = await GenerateJournalEntryNumber();

        // Create the reversing entry with opposite amounts
        var reversingEntry = new JournalEntry
        {
            EntryNumber = reversingEntryNumber,
            EntryDate = DateTime.Now,
            PostingDate = DateTime.Now,
            EntryType = "Reversal",
            Reference = $"Reverses {originalEntry.EntryNumber}",
            Description = $"Reversal of {originalEntry.EntryNumber}: {originalEntry.Description}",
            TotalDebit = originalEntry.TotalDebit,
            TotalCredit = originalEntry.TotalCredit,
            Status = "Posted", // Reversing entries are auto-posted
            Source_Table = originalEntry.Source_Table,
            Source_ID = originalEntry.Source_ID,
            ReversesEntry_ID = originalEntry.JournalEntryID, // Link to original
            CreatedBy = userId,
            CreatedDate = DateTime.Now,
            PostedBy = userId,
            PostedDate = DateTime.Now
        };

        if (!await _journalEntryRepo.Insert(reversingEntry))
            return false;

        // Create reversed lines (swap debit/credit)
        int lineNumber = 1;
        foreach (var originalLine in originalEntry.JournalEntryLines)
        {
            var reversedLine = new JournalEntryLine
            {
                JournalEntry_ID = reversingEntry.JournalEntryID,
                LineNumber = lineNumber++,
                Account_ID = originalLine.Account_ID,
                DebitAmount = originalLine.CreditAmount,  // Swap: original credit becomes debit
                CreditAmount = originalLine.DebitAmount,  // Swap: original debit becomes credit
                Description = $"Reversal: {originalLine.Description}",
                Store_ID = originalLine.Store_ID
            };

            await _journalEntryLineRepo.Insert(reversedLine);
        }

        // Mark the original entry as Void and link to the reversing entry
        originalEntry.Status = "Void";
        originalEntry.ReversedByEntry_ID = reversingEntry.JournalEntryID;
        originalEntry.UpdatedBy = userId;
        originalEntry.UpdatedDate = DateTime.Now;

        return await _journalEntryRepo.Update(originalEntry);
    }

    public async Task<JournalEntryDto?> GetJournalEntryById(int journalEntryId)
    {
        var entry = await _journalEntryRepo.FindByCondition(je => je.JournalEntryID == journalEntryId)
            .Include(je => je.JournalEntryLines)
                .ThenInclude(jel => jel.Account)
            .Include(je => je.ReversesEntry)
            .Include(je => je.ReversedByEntry)
            .FirstOrDefaultAsync();

        if (entry == null) return null;

        return new JournalEntryDto
        {
            JournalEntryID = entry.JournalEntryID,
            EntryNumber = entry.EntryNumber,
            EntryDate = entry.EntryDate,
            PostingDate = entry.PostingDate,
            EntryType = entry.EntryType,
            Reference = entry.Reference,
            Description = entry.Description,
            TotalDebit = entry.TotalDebit,
            TotalCredit = entry.TotalCredit,
            Status = entry.Status,
            Source_Table = entry.Source_Table,
            Source_ID = entry.Source_ID,
            // Reversal tracking
            ReversesEntry_ID = entry.ReversesEntry_ID,
            ReversesEntryNumber = entry.ReversesEntry?.EntryNumber,
            ReversedByEntry_ID = entry.ReversedByEntry_ID,
            ReversedByEntryNumber = entry.ReversedByEntry?.EntryNumber,
            Lines = entry.JournalEntryLines.OrderBy(l => l.LineNumber).Select(l => new JournalEntryLineDto
            {
                JournalEntryLineID = l.JournalEntryLineID,
                LineNumber = l.LineNumber,
                Account_ID = l.Account_ID,
                AccountName = l.Account?.AccountName,
                DebitAmount = l.DebitAmount,
                CreditAmount = l.CreditAmount,
                Description = l.Description,
                Store_ID = l.Store_ID
            }).ToList()
        };
    }

    public async Task<List<JournalEntryDto>> GetJournalEntries(DateTime? fromDate = null, DateTime? toDate = null, string? status = null, string? entryType = null)
    {
        var query = _journalEntryRepo.GetAllWithInclude(je => je.JournalEntryLines);

        if (fromDate.HasValue)
            query = query.Where(je => je.EntryDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(je => je.EntryDate <= toDate.Value);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(je => je.Status == status);

        if (!string.IsNullOrEmpty(entryType))
            query = query.Where(je => je.EntryType == entryType);

        var entries = await query.OrderByDescending(je => je.EntryDate).ToListAsync();

        var result = new List<JournalEntryDto>();
        foreach (var entry in entries)
        {
            var dto = await GetJournalEntryById(entry.JournalEntryID);
            if (dto != null)
                result.Add(dto);
        }

        return result;
    }

    public async Task<string> GenerateJournalEntryNumber()
    {
        return UniqueIdGenerator.Generate("JE");
    }

    // ==================== BALANCE & REPORTING ====================

    public async Task<decimal> GetAccountBalance(int accountId, DateTime? asOfDate = null)
    {
        var account = await GetAccountById(accountId);
        if (account == null) return 0;

        var asOfEndOfDay = asOfDate?.Date.AddDays(1).AddTicks(-1);

        var lines = await _journalEntryLineRepo.FindByCondition(jel => jel.Account_ID == accountId)
            .Include(jel => jel.JournalEntry)
            .Where(jel => jel.JournalEntry != null && jel.JournalEntry.Status == "Posted" &&
                         (!asOfEndOfDay.HasValue || jel.JournalEntry.PostingDate <= asOfEndOfDay.Value))
            .ToListAsync();

        var totalDebits = lines.Sum(l => l.DebitAmount);
        var totalCredits = lines.Sum(l => l.CreditAmount);

        // Determine normal balance based on Head's Family
        var family = account.Head?.Family ?? "Assets";
        bool isDebitNormal = family == "Assets" || family == "Expense";

        if (isDebitNormal)
            return totalDebits - totalCredits;
        else
            return totalCredits - totalDebits;
    }

    public async Task<decimal> GetAccountBalanceByCode(string accountCode, DateTime? asOfDate = null)
    {
        // This method is deprecated since AccountCode was removed
        return 0;
    }

    public async Task<TrialBalanceDto> GetTrialBalance(DateTime asOfDate, int? storeId = null)
    {
        var accounts = await GetChartOfAccounts(true);
        var trialBalance = new TrialBalanceDto
        {
            AsOfDate = asOfDate,
            StoreId = storeId,
            Lines = new List<TrialBalanceLineDto>()
        };

        // NOTE: StoreName can be populated by the caller if needed

        foreach (var account in accounts)
        {
            var balance = await GetAccountBalanceWithStore(account.AccountID, asOfDate, storeId);

            if (Math.Abs(balance) > 0.01m) // Only include accounts with balance
            {
                var head = await _headRepo.FindByCondition(h => h.HeadID == account.Head_ID).FirstOrDefaultAsync();
                var family = head?.Family ?? "Assets";
                bool isDebitNormal = family == "Assets" || family == "Expense";

                var line = new TrialBalanceLineDto
                {
                    AccountCode = account.AccountNo ?? "",
                    AccountName = account.AccountName,
                    Family = family
                };

                if (isDebitNormal)
                {
                    line.DebitBalance = balance > 0 ? balance : 0;
                    line.CreditBalance = balance < 0 ? Math.Abs(balance) : 0;
                }
                else
                {
                    line.DebitBalance = balance < 0 ? Math.Abs(balance) : 0;
                    line.CreditBalance = balance > 0 ? balance : 0;
                }

                trialBalance.Lines.Add(line);
            }
        }

        trialBalance.TotalDebits = trialBalance.Lines.Sum(l => l.DebitBalance);
        trialBalance.TotalCredits = trialBalance.Lines.Sum(l => l.CreditBalance);

        return trialBalance;
    }

    /// <summary>
    /// Get account balance filtered by store
    /// </summary>
    private async Task<decimal> GetAccountBalanceWithStore(int accountId, DateTime? asOfDate, int? storeId)
    {
        var queryEnd = asOfDate?.Date.AddDays(1).AddTicks(-1) ?? DateTime.MaxValue;

        var query = _journalEntryLineRepo.FindByCondition(jel => jel.Account_ID == accountId)
            .Include(jel => jel.JournalEntry)
            .Where(jel => jel.JournalEntry != null && jel.JournalEntry.Status == "Posted" &&
                         jel.JournalEntry.PostingDate <= queryEnd);

        // Apply store filter if specified
        if (storeId.HasValue)
        {
            query = query.Where(jel => jel.JournalEntry!.Store_ID == storeId.Value);
        }

        var lines = await query.ToListAsync();

        var totalDebits = lines.Sum(l => l.DebitAmount);
        var totalCredits = lines.Sum(l => l.CreditAmount);

        var account = await GetAccountById(accountId);
        var family = account?.Head?.Family ?? "Assets";
        bool isDebitNormal = family == "Assets" || family == "Expense";

        return isDebitNormal ? totalDebits - totalCredits : totalCredits - totalDebits;
    }

    public async Task<BalanceSheetDto> GetBalanceSheet(DateTime asOfDate, int? storeId = null)
    {
        var accounts = await GetChartOfAccounts(true);
        var balanceSheet = new BalanceSheetDto
        {
            AsOfDate = asOfDate
        };

        foreach (var account in accounts)
        {
            var balance = await GetAccountBalanceWithStore(account.AccountID, asOfDate, storeId);
            if (Math.Abs(balance) < 0.01m) continue;

            var head = await _headRepo.FindByCondition(h => h.HeadID == account.Head_ID).FirstOrDefaultAsync();
            var family = head?.Family ?? "Assets";

            var line = new BalanceSheetSectionDto
            {
                AccountName = account.AccountName,
                Amount = Math.Abs(balance)
            };

            if (family == "Assets")
            {
                balanceSheet.Assets.Add(line);
                balanceSheet.TotalAssets += line.Amount;
            }
            else if (family == "Liability")
            {
                balanceSheet.Liabilities.Add(line);
                balanceSheet.TotalLiabilities += line.Amount;
            }
            else if (family == "Capital")
            {
                balanceSheet.Equity.Add(line);
                balanceSheet.TotalEquity += line.Amount;
            }
        }

        return balanceSheet;
    }

    public async Task<IncomeStatementDto> GetIncomeStatement(DateTime fromDate, DateTime toDate, int? storeId = null)
    {
        var start = fromDate.Date;
        var end = toDate.Date.AddDays(1).AddTicks(-1);

        var incomeStatement = new IncomeStatementDto
        {
            FromDate = start,
            ToDate = toDate.Date
        };

        // Get all posted lines for the period
        var query = _journalEntryLineRepo.GetAllWithInclude(jel => jel.JournalEntry, jel => jel.Account, jel => jel.Account!.Head)
            .Where(jel => jel.JournalEntry != null && jel.JournalEntry.Status == "Posted" &&
                         jel.JournalEntry.PostingDate >= start && jel.JournalEntry.PostingDate <= end);

        // Apply store filter if specified
        if (storeId.HasValue)
        {
            query = query.Where(jel => jel.JournalEntry!.Store_ID == storeId.Value);
        }

        var periodLines = await query.ToListAsync();

        // Group by account
        var accountSums = periodLines
            .GroupBy(l => l.Account_ID)
            .Select(g => new
            {
                AccountID = g.Key,
                Account = g.First().Account,
                TotalDebit = g.Sum(l => l.DebitAmount),
                TotalCredit = g.Sum(l => l.CreditAmount)
            })
            .ToList();

        foreach (var item in accountSums)
        {
            var account = item.Account;
            if (account == null) continue;

            var family = account.Head?.Family ?? string.Empty;

            // Calculate balance based on normal balance
            bool isDebitNormal = family == "Expense";
            decimal balance;
            if (isDebitNormal)
                balance = item.TotalDebit - item.TotalCredit;
            else
                balance = item.TotalCredit - item.TotalDebit;

            if (Math.Abs(balance) < 0.01m) continue;

            var line = new IncomeStatementLineDto
            {
                AccountName = account.AccountName,
                Amount = Math.Abs(balance)
            };

            if (family == "Revenue")
            {
                incomeStatement.Revenue.Add(line);
                incomeStatement.TotalRevenue += line.Amount;
            }
            else if (family == "Expense")
            {
                incomeStatement.Expenses.Add(line);
                incomeStatement.TotalExpenses += line.Amount;
            }
        }

        incomeStatement.GrossProfit = incomeStatement.TotalRevenue - incomeStatement.TotalCOGS;
        incomeStatement.NetIncome = incomeStatement.GrossProfit - incomeStatement.TotalExpenses;

        return incomeStatement;
    }

    public async Task<GeneralLedgerDto> GetGeneralLedger(int accountId, DateTime? fromDate = null, DateTime? toDate = null, int? storeId = null)
    {
        var account = await GetAccountById(accountId);
        if (account == null)
            throw new ArgumentException("Account not found", nameof(accountId));

        var ledger = new GeneralLedgerDto
        {
            Account = MapToDto(account),
            FromDate = fromDate,
            ToDate = toDate
        };

        // Determine normal balance based on family
        var family = account.Head?.Family ?? "Assets";
        bool isDebitNormal = family == "Assets" || family == "Expense";

        // Get opening balance
        if (fromDate.HasValue)
        {
            var startOfPeriod = fromDate.Value.Date;
            var openingQuery = _journalEntryLineRepo.FindByCondition(jel => jel.Account_ID == accountId)
                .Include(jel => jel.JournalEntry)
                .Where(jel => jel.JournalEntry != null && jel.JournalEntry.Status == "Posted" &&
                             jel.JournalEntry.PostingDate < startOfPeriod);

            if (storeId.HasValue)
            {
                openingQuery = openingQuery.Where(jel => jel.JournalEntry!.Store_ID == storeId.Value);
            }

            var linesBefore = await openingQuery.ToListAsync();

            var totalDebitsBefore = linesBefore.Sum(l => l.DebitAmount);
            var totalCreditsBefore = linesBefore.Sum(l => l.CreditAmount);

            if (isDebitNormal)
                ledger.OpeningBalance = totalDebitsBefore - totalCreditsBefore;
            else
                ledger.OpeningBalance = totalCreditsBefore - totalDebitsBefore;
        }

        // Get transactions for the period
        var queryStart = fromDate?.Date ?? DateTime.MinValue;
        var queryEnd = toDate?.Date.AddDays(1).AddTicks(-1) ?? DateTime.MaxValue;

        var periodQuery = _journalEntryLineRepo.FindByCondition(jel => jel.Account_ID == accountId)
            .Include(jel => jel.JournalEntry)
            .Where(jel => jel.JournalEntry != null && jel.JournalEntry.Status == "Posted" &&
                         jel.JournalEntry.PostingDate >= queryStart && jel.JournalEntry.PostingDate <= queryEnd);

        if (storeId.HasValue)
        {
            periodQuery = periodQuery.Where(jel => jel.JournalEntry!.Store_ID == storeId.Value);
        }

        var lines = await periodQuery
            .OrderBy(jel => jel.JournalEntry != null ? jel.JournalEntry.PostingDate : DateTime.MinValue)
            .ToListAsync();

        decimal runningBalance = ledger.OpeningBalance;

        foreach (var line in lines)
        {
            if (isDebitNormal)
                runningBalance += line.DebitAmount - line.CreditAmount;
            else
                runningBalance += line.CreditAmount - line.DebitAmount;

            ledger.Transactions.Add(new GeneralLedgerLineDto
            {
                Date = line.JournalEntry?.PostingDate ?? DateTime.Now,
                EntryNumber = line.JournalEntry?.EntryNumber ?? "",
                Description = line.Description ?? line.JournalEntry?.Description ?? "",
                DebitAmount = line.DebitAmount,
                CreditAmount = line.CreditAmount,
                Balance = runningBalance
            });
        }

        ledger.ClosingBalance = runningBalance;

        return ledger;
    }

    // ==================== VALIDATION ====================

    public async Task<(bool IsValid, List<string> Errors)> ValidateJournalEntry(JournalEntryDto dto)
    {
        var errors = new List<string>();

        // Must have at least 2 lines
        if (dto.Lines == null || dto.Lines.Count < 2)
            errors.Add("Journal entry must have at least 2 lines");

        if (dto.Lines != null)
        {
            // Total debits must equal total credits
            var totalDebits = dto.Lines.Sum(l => l.DebitAmount);
            var totalCredits = dto.Lines.Sum(l => l.CreditAmount);

            if (Math.Abs(totalDebits - totalCredits) > 0.01m)
                errors.Add($"Debits ({totalDebits:N2}) must equal Credits ({totalCredits:N2})");

            // Each line must have either debit OR credit (not both, not neither)
            foreach (var line in dto.Lines)
            {
                if (line.DebitAmount > 0 && line.CreditAmount > 0)
                    errors.Add("A line cannot have both debit and credit amounts");

                if (line.DebitAmount == 0 && line.CreditAmount == 0)
                    errors.Add("A line must have either a debit or credit amount");

                // Account must exist
                int accountId = line.Account_ID ?? 0;

                if (accountId == 0)
                    errors.Add("Invalid account specified");
                else
                {
                    var account = await GetAccountById(accountId);
                    if (account == null)
                        errors.Add($"Account ID {accountId} not found");
                    else if (!account.IsActive)
                        errors.Add($"Account {account.AccountName} is inactive");
                }
            }
        }

        return (errors.Count == 0, errors);
    }

    // ==================== HELPER METHODS ====================

    private ChartOfAccountDto MapToDto(ChartOfAccount account)
    {
        return new ChartOfAccountDto
        {
            AccountID = account.AccountID,
            AccountName = account.AccountName,
            Head_ID = account.Head_ID,
            HeadName = account.Head?.HeadName ?? "",
            Subhead_ID = account.Subhead_ID,
            SubheadName = account.Subhead?.SubheadName ?? "",
            AccountType_ID = account.AccountType_ID,
            AccountType = account.AccountType?.TypeName ?? "",
            AccountNo = account.AccountNo,
            IBAN = account.IBAN,
            AccountAddress = account.AccountAddress,
            IsActive = account.IsActive
        };
    }
}
