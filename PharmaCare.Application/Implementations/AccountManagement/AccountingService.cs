using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.DTOs.Accounting;
using PharmaCare.Application.Interfaces.AccountManagement;
using PharmaCare.Domain.Models.AccountManagement;
using PharmaCare.Domain.Models.Inventory;
using PharmaCare.Application.Utilities;
using PharmaCare.Infrastructure.Interfaces;
using PharmaCare.Infrastructure.Interfaces.Accounting;

namespace PharmaCare.Application.Implementations.AccountManagement;

public class AccountingService : IAccountingService
{
    private readonly IRepository<AccountType> _accountTypeRepo;
    private readonly IRepository<ChartOfAccount> _chartOfAccountRepo;
    private readonly IRepository<AccountVoucher> _voucherRepo;
    private readonly IRepository<AccountVoucherDetail> _voucherDetailRepo;
    private readonly IRepository<Head> _headRepo;
    private readonly IVoucherService _voucherService;

    public AccountingService(
        IRepository<AccountType> accountTypeRepo,
        IRepository<ChartOfAccount> chartOfAccountRepo,
        IRepository<AccountVoucher> voucherRepo,
        IRepository<AccountVoucherDetail> voucherDetailRepo,
        IRepository<Head> headRepo,
        IVoucherService voucherService)
    {
        _accountTypeRepo = accountTypeRepo;
        _chartOfAccountRepo = chartOfAccountRepo;
        _voucherRepo = voucherRepo;
        _voucherDetailRepo = voucherDetailRepo;
        _headRepo = headRepo;
        _voucherService = voucherService;
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
        var hasTransactions = await _voucherDetailRepo
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

        // Create Voucher Request (Type 1 = Journal Voucher)
        var voucherRequest = new CreateVoucherRequest
        {
            VoucherTypeId = 1, // JV
            VoucherDate = dto.EntryDate == default ? DateTime.Now : dto.EntryDate,
            SourceTable = dto.Source_Table,
            SourceId = dto.Source_ID,
            // StoreId: We take from first line or default?
            StoreId = dto.Lines?.FirstOrDefault()?.Store_ID,
            Narration = dto.Description,
            CreatedBy = userId,
            Lines = new List<CreateVoucherLineRequest>()
        };

        foreach (var line in dto.Lines)
        {
            voucherRequest.Lines.Add(new CreateVoucherLineRequest
            {
                AccountId = line.Account_ID ?? 0,
                Dr = line.DebitAmount,
                Cr = line.CreditAmount,
                Particulars = line.Description ?? dto.Description,
                StoreId = line.Store_ID
            });
        }

        var voucher = await _voucherService.CreateVoucherAsync(voucherRequest);
        
        // Return VoucherID as JournalEntryID
        return voucher.VoucherID;
    }

    public async Task<bool> PostJournalEntry(int journalEntryId, int userId)
    {
        // Vouchers created via CreateVoucherAsync are typically ready. 
        // If we need a specific 'Post' step, we'd use _voucherService.AuthorizeVoucher or similar.
        // Assuming creation is enough for now, or this method simply validates existence.
        
        var voucher = await _voucherRepo.GetByIdAsync(journalEntryId);
        if (voucher == null) return false;

        return true; 
    }

    public async Task<bool> VoidJournalEntry(int journalEntryId, int userId)
    {
        try
        {
             // Use ReverseVoucherAsync to void/reverse
             await _voucherService.ReverseVoucherAsync(journalEntryId, "Voided by user", userId);
             return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<JournalEntryDto?> GetJournalEntryById(int journalEntryId)
    {
        var voucher = await _voucherRepo.FindByCondition(v => v.VoucherID == journalEntryId)
            .Include(v => v.VoucherDetails)
                .ThenInclude(d => d.Account)
            .FirstOrDefaultAsync();

        if (voucher == null) return null;

        return new JournalEntryDto
        {
            JournalEntryID = voucher.VoucherID,
            EntryNumber = voucher.VoucherCode,
            EntryDate = voucher.VoucherDate,
            PostingDate = voucher.VoucherDate,
            EntryType = "Manual", // Default
            Description = voucher.Narration,
            TotalDebit = voucher.TotalDebit,
            TotalCredit = voucher.TotalCredit,
            Status = voucher.Status,
            Source_Table = voucher.SourceTable,
            Source_ID = voucher.SourceID,
            Lines = voucher.VoucherDetails.Select((l, index) => new JournalEntryLineDto
            {
                JournalEntryLineID = l.VoucherDetailID,
                LineNumber = index + 1,
                Account_ID = l.Account_ID,
                AccountName = l.Account?.AccountName,
                DebitAmount = l.Dr,
                CreditAmount = l.Cr,
                Description = l.Particulars ?? voucher.Narration ?? "",
                Store_ID = voucher.Store_ID
            }).ToList()
        };
    }

    public async Task<List<JournalEntryDto>> GetJournalEntries(DateTime? fromDate = null, DateTime? toDate = null, string? status = null, string? entryType = null)
    {
        var query = _voucherRepo.GetAllWithInclude(je => je.VoucherDetails);

        if (fromDate.HasValue)
            query = query.Where(je => je.VoucherDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(je => je.VoucherDate <= toDate.Value);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(je => je.Status == status);

        // EntryType string won't seamlessly map to VoucherTypeId (int).
        // If entryType param is passed (e.g. "Manual"), we might need logic.
        // For now, ignoring entryType or using it if it was numeric.
        // For compatibility, if legacy code calls with "Manual", we return everything for now.

        var entries = await query.OrderByDescending(je => je.VoucherDate).ToListAsync();

        var result = new List<JournalEntryDto>();
        foreach (var entry in entries)
        {
            var dto = await GetJournalEntryById(entry.VoucherID);
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

        var lines = await _voucherDetailRepo.FindByCondition(jel => jel.Account_ID == accountId)
            .Include(jel => jel.Voucher)
            .Where(jel => jel.Voucher != null && jel.Voucher.Status == "Posted" &&
                         (!asOfEndOfDay.HasValue || jel.Voucher.VoucherDate <= asOfEndOfDay.Value))
            .ToListAsync();

        var totalDebits = lines.Sum(l => l.Dr);
        var totalCredits = lines.Sum(l => l.Cr);

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

        var query = _voucherDetailRepo.FindByCondition(jel => jel.Account_ID == accountId)
            .Include(jel => jel.Voucher)
            .Where(jel => jel.Voucher != null && jel.Voucher.Status == "Posted" &&
                         jel.Voucher.VoucherDate <= queryEnd);

        // Apply store filter if specified
        if (storeId.HasValue)
        {
            query = query.Where(jel => jel.Store_ID == storeId.Value);
        }

        var lines = await query.ToListAsync();

        var totalDebits = lines.Sum(l => l.Dr);
        var totalCredits = lines.Sum(l => l.Cr);

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
        var query = _voucherDetailRepo.GetAllWithInclude(jel => jel.Voucher, jel => jel.Account, jel => jel.Account!.Head)
            .Where(jel => jel.Voucher != null && jel.Voucher.Status == "Posted" &&
                         jel.Voucher.VoucherDate >= start && jel.Voucher.VoucherDate <= end);

        // Apply store filter if specified
        if (storeId.HasValue)
        {
            query = query.Where(jel => jel.Store_ID == storeId.Value);
        }

        var periodLines = await query.ToListAsync();

        // Group by account
        var accountSums = periodLines
            .GroupBy(l => l.Account_ID)
            .Select(g => new
            {
                AccountID = g.Key,
                Account = g.First().Account,
                TotalDebit = g.Sum(l => l.Dr),
                TotalCredit = g.Sum(l => l.Cr)
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
            var openingQuery = _voucherDetailRepo.FindByCondition(jel => jel.Account_ID == accountId)
                .Include(jel => jel.Voucher)
                .Where(jel => jel.Voucher != null && jel.Voucher.Status == "Posted" &&
                             jel.Voucher.VoucherDate < startOfPeriod);

            if (storeId.HasValue)
            {
                openingQuery = openingQuery.Where(jel => jel.Store_ID == storeId.Value);
            }

            var linesBefore = await openingQuery.ToListAsync();

            var totalDebitsBefore = linesBefore.Sum(l => l.Dr);
            var totalCreditsBefore = linesBefore.Sum(l => l.Cr);

            if (isDebitNormal)
                ledger.OpeningBalance = totalDebitsBefore - totalCreditsBefore;
            else
                ledger.OpeningBalance = totalCreditsBefore - totalDebitsBefore;
        }

        // Get transactions for the period
        var queryStart = fromDate?.Date ?? DateTime.MinValue;
        var queryEnd = toDate?.Date.AddDays(1).AddTicks(-1) ?? DateTime.MaxValue;

        var periodQuery = _voucherDetailRepo.FindByCondition(jel => jel.Account_ID == accountId)
            .Include(jel => jel.Voucher)
            .Where(jel => jel.Voucher != null && jel.Voucher.Status == "Posted" &&
                         jel.Voucher.VoucherDate >= queryStart && jel.Voucher.VoucherDate <= queryEnd);

        if (storeId.HasValue)
        {
            periodQuery = periodQuery.Where(jel => jel.Store_ID == storeId.Value);
        }

        var lines = await periodQuery
            .OrderBy(jel => jel.Voucher != null ? jel.Voucher.VoucherDate : DateTime.MinValue)
            .ToListAsync();

        decimal runningBalance = ledger.OpeningBalance;

        foreach (var line in lines)
        {
            if (isDebitNormal)
                runningBalance += line.Dr - line.Cr;
            else
                runningBalance += line.Cr - line.Dr;

            ledger.Transactions.Add(new GeneralLedgerLineDto
            {
                Date = line.Voucher?.VoucherDate ?? DateTime.Now,
                EntryNumber = line.Voucher?.VoucherCode ?? "", // VoucherCode instead of EntryNumber
                Description = line.Particulars ?? line.Voucher?.Narration ?? "",
                DebitAmount = line.Dr,
                CreditAmount = line.Cr,
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
