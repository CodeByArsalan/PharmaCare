using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Utilities;
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Domain.Entities.Accounting;
using PharmaCare.Domain.Entities.Configuration;
using PharmaCare.Domain.Entities.Finance;
using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.Application.Implementations.Accounting;

public class AccountService : IAccountService
{
    private readonly IRepository<Account> _repository;
    private readonly IRepository<AccountSubhead> _subheadRepository;
    private readonly IRepository<AccountHead> _headRepository;
    private readonly IRepository<AccountType> _typeRepository;
    private readonly IRepository<VoucherDetail> _voucherDetailRepository;
    private readonly IRepository<Category> _categoryRepository;
    private readonly IRepository<Party> _partyRepository;
    private readonly IRepository<ExpenseCategory> _expenseCategoryRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AccountService(
        IRepository<Account> repository,
        IRepository<AccountSubhead> subheadRepository,
        IRepository<AccountHead> headRepository,
        IRepository<AccountType> typeRepository,
        IRepository<VoucherDetail> voucherDetailRepository,
        IRepository<Category> categoryRepository,
        IRepository<Party> partyRepository,
        IRepository<ExpenseCategory> expenseCategoryRepository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _subheadRepository = subheadRepository;
        _headRepository = headRepository;
        _typeRepository = typeRepository;
        _voucherDetailRepository = voucherDetailRepository;
        _categoryRepository = categoryRepository;
        _partyRepository = partyRepository;
        _expenseCategoryRepository = expenseCategoryRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<Account>> GetAllAsync()
    {
        return await _repository.Query()
            .Include(a => a.AccountSubhead)
            .Include(a => a.AccountHead)
            .Include(a => a.AccountType)
            .OrderBy(a => a.AccountID)
            .ToListAsync();
    }

    public async Task<Account?> GetByIdAsync(int id)
    {
        return await _repository.Query()
            .Include(a => a.AccountSubhead)
            .Include(a => a.AccountHead)
            .Include(a => a.AccountType)
            .FirstOrDefaultAsync(a => a.AccountID == id);
    }

    public async Task<Account> CreateAsync(Account account, int userId)
    {
        account.CreatedAt = AppTime.Now;
        account.CreatedBy = userId;
        account.IsActive = true; 

        await _repository.AddAsync(account);
        await _unitOfWork.SaveChangesAsync();
        return account;
    }

    public async Task<bool> UpdateAsync(Account account, int userId)
    {
        var existing = await _repository.FirstOrDefaultAsync(a => a.AccountID == account.AccountID);
        if (existing == null) return false;

        // An account's CLASSIFICATION is what the rest of the system reasons about. Every
        // "is this really cash?" gate — customer receipts, supplier payments, refunds — is a lookup
        // on AccountType, so re-typing a receivable as CASH turns it into a valid tender account and
        // defeats all of those at once. Its head and subhead decide where it lands on the financial
        // statements. None of that may move once the account carries history.
        // Read as a projection, not off `existing`: a caller that passes the tracked instance it
        // already mutated would make the two sides of this comparison the same object, and the
        // guard would never fire.
        var stored = await _repository.Query()
            .AsNoTracking()
            .Where(a => a.AccountID == account.AccountID)
            .Select(a => new { a.AccountType_ID, a.AccountHead_ID, a.AccountSubhead_ID, a.IsSystemAccount })
            .FirstOrDefaultAsync();

        if (stored == null) return false;

        var classificationChanged =
            stored.AccountType_ID != account.AccountType_ID ||
            stored.AccountHead_ID != account.AccountHead_ID ||
            stored.AccountSubhead_ID != account.AccountSubhead_ID;

        if (classificationChanged)
        {
            if (stored.IsSystemAccount)
            {
                throw new InvalidOperationException(
                    $"'{existing.Name}' is a system account. Its classification is fixed because the " +
                    "posting engine resolves it by type.");
            }

            if (await IsPartyLedgerAccountAsync(existing.AccountID))
            {
                throw new InvalidOperationException(
                    $"'{existing.Name}' is the ledger account of a customer or supplier. Its " +
                    "classification is owned by that party's type, not by this screen.");
            }

            if (await HasPostedEntriesAsync(existing.AccountID))
            {
                throw new InvalidOperationException(
                    $"'{existing.Name}' already has posted entries, so it cannot be reclassified — " +
                    "every report covering those entries would change retrospectively. " +
                    "Create a new account under the correct classification instead.");
            }
        }

        existing.Name = account.Name;
        existing.AccountHead_ID = account.AccountHead_ID;
        existing.AccountSubhead_ID = account.AccountSubhead_ID;
        existing.AccountType_ID = account.AccountType_ID;
        // IsSystemAccount is RESTORED to its stored value rather than merely "not copied". It is the
        // flag that protects the provisioned chart of accounts from exactly the edits guarded
        // above, so the edit form must not be able to grant or revoke it. Simply omitting the
        // assignment is not enough: when the caller hands us the tracked entity it has already
        // written to, the change is sitting in the change tracker and SaveChanges would persist it.
        existing.IsSystemAccount = stored.IsSystemAccount;
        existing.IsActive = account.IsActive;

        existing.UpdatedAt = AppTime.Now;
        existing.UpdatedBy = userId;

        _repository.Update(existing);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ToggleStatusAsync(int id, int userId)
    {
        var account = await _repository.FirstOrDefaultAsync(a => a.AccountID == id);
        if (account == null) return false;

        // Deactivating is only ever meant to retire an account nothing points at any more. Master
        // data still referencing it keeps right on posting — the postings do not check IsActive —
        // so the balance stays live while disappearing from every screen that filters on it.
        if (account.IsActive)
        {
            if (account.IsSystemAccount)
            {
                throw new InvalidOperationException(
                    $"'{account.Name}' is a system account and cannot be deactivated.");
            }

            var user = await DescribeReferenceAsync(id);
            if (user != null)
            {
                throw new InvalidOperationException(
                    $"'{account.Name}' cannot be deactivated because {user} still posts to it. " +
                    "Re-point that first.");
            }
        }

        account.IsActive = !account.IsActive;
        account.UpdatedAt = AppTime.Now;
        account.UpdatedBy = userId;

        _repository.Update(account);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    private Task<bool> HasPostedEntriesAsync(int accountId)
        => _voucherDetailRepository.Query().AsNoTracking()
            .AnyAsync(d => d.Account_ID == accountId);

    private Task<bool> IsPartyLedgerAccountAsync(int accountId)
        => _partyRepository.Query().AsNoTracking()
            .AnyAsync(p => p.Account_ID == accountId);

    /// <summary>
    /// Names the master data still pointing at this account, or null when nothing does.
    /// </summary>
    private async Task<string?> DescribeReferenceAsync(int accountId)
    {
        var category = await _categoryRepository.Query().AsNoTracking()
            .FirstOrDefaultAsync(c => c.SaleAccount_ID == accountId
                                   || c.StockAccount_ID == accountId
                                   || c.COGSAccount_ID == accountId
                                   || c.DamageAccount_ID == accountId);
        if (category != null)
            return $"the category '{category.Name}'";

        var party = await _partyRepository.Query().AsNoTracking()
            .FirstOrDefaultAsync(p => p.Account_ID == accountId);
        if (party != null)
            return $"the {party.PartyType.ToLowerInvariant()} '{party.Name}'";

        var expenseCategory = await _expenseCategoryRepository.Query().AsNoTracking()
            .FirstOrDefaultAsync(e => e.DefaultExpenseAccount_ID == accountId);
        if (expenseCategory != null)
            return $"the expense category '{expenseCategory.Name}'";

        return null;
    }

    public async Task<IEnumerable<AccountSubhead>> GetSubHeadsForDropdownAsync()
    {
        return await _subheadRepository.Query()
            .OrderBy(s => s.SubheadName)
            .ToListAsync();
    }

    public async Task<IEnumerable<AccountType>> GetAccountTypesForDropdownAsync()
    {
        return await _typeRepository.Query()
            .OrderBy(t => t.Name)
            .ToListAsync();
    }
    public async Task<IEnumerable<AccountHead>> GetAccountHeadsForDropdownAsync()
    {
        return await _headRepository.Query()
            .OrderBy(h => h.HeadName)
            .ToListAsync();
    }

    public async Task<IEnumerable<AccountSubhead>> GetSubHeadsByHeadIdAsync(int headId)
    {
        return await _subheadRepository.Query()
            .Where(s => s.AccountHead_ID == headId)
            .OrderBy(s => s.SubheadName)
            .ToListAsync();
    }

    public async Task<IEnumerable<Account>> GetCashBankAccountsAsync()
    {
        // Combined list of Cash and Bank accounts for initial load or general selection
        var cashAccounts = await GetAccountsByMethodAsync("Cash");
        var bankAccounts = await GetAccountsByMethodAsync("Bank");
        
        return cashAccounts.Concat(bankAccounts)
            .OrderBy(a => a.Name)
            .ToList();
    }

    public async Task<IEnumerable<Account>> GetAccountsByMethodAsync(string method)
    {
        var accounts = await GetAllAsync();
        
        if (string.IsNullOrWhiteSpace(method))
            return Enumerable.Empty<Account>();

        return accounts.Where(a => 
        {
            if (a.AccountType == null) return false;

            var typeName = a.AccountType.Name.ToLower();
            var typeCode = a.AccountType.Code.ToLower();
            var methodLower = method.ToLower();

            // Unified logic for Bank/Cheque
            if (methodLower == "bank" || methodLower == "cheque")
            {
                return typeName.Contains("bank") || typeCode.Contains("bank") || a.AccountType_ID == AccountingConstants.BankAccountTypeId;
            }
            
            // Logic for Cash
            if (methodLower == "cash")
            {
                return typeName.Contains("cash") || typeCode.Contains("cash") || a.AccountType_ID == AccountingConstants.CashAccountTypeId;
            }

            return false;
        })
        .Where(a => a.IsActive)
        .OrderBy(a => a.Name)
        .ToList();
    }
}
