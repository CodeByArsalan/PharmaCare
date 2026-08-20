using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.DTOs;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Domain.Entities.Configuration;
using PharmaCare.Application.Interfaces.Logging;
using PharmaCare.Domain.Enums;

namespace PharmaCare.Application.Implementations.Configuration;

public class PartyService : IPartyService
{
    // Well-known chart-of-accounts codes resolved within the current pharmacy (tenant).
    // Seeded per pharmacy at provisioning; replaces the old hardcoded account IDs.
    private const string CustomerHeadCode = "AR_HEAD";
    private const string CustomerSubheadCode = "AR_SUB";
    private const string CustomerTypeCode = "AR";
    private const string SupplierHeadCode = "AP_HEAD";
    private const string SupplierSubheadCode = "AP_SUB";
    private const string SupplierTypeCode = "AP";

    private readonly IRepository<Party> _repository;
    private readonly IRepository<PharmaCare.Domain.Entities.Accounting.Account> _accountRepository;
    private readonly IRepository<PharmaCare.Domain.Entities.Accounting.AccountHead> _accountHeadRepository;
    private readonly IRepository<PharmaCare.Domain.Entities.Accounting.AccountSubhead> _accountSubheadRepository;
    private readonly IRepository<PharmaCare.Domain.Entities.Accounting.AccountType> _accountTypeRepository;
    private readonly IActivityLogService _activityLogService;
    private readonly ISessionService _sessionService;
    private readonly IOpeningBalanceService _openingBalanceService;
    private readonly IUnitOfWork _unitOfWork;

    public PartyService(
        IRepository<Party> repository,
        IRepository<PharmaCare.Domain.Entities.Accounting.Account> accountRepository,
        IRepository<PharmaCare.Domain.Entities.Accounting.AccountHead> accountHeadRepository,
        IRepository<PharmaCare.Domain.Entities.Accounting.AccountSubhead> accountSubheadRepository,
        IRepository<PharmaCare.Domain.Entities.Accounting.AccountType> accountTypeRepository,
        IActivityLogService activityLogService,
        ISessionService sessionService,
        IOpeningBalanceService openingBalanceService,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _accountRepository = accountRepository;
        _accountHeadRepository = accountHeadRepository;
        _accountSubheadRepository = accountSubheadRepository;
        _accountTypeRepository = accountTypeRepository;
        _activityLogService = activityLogService;
        _sessionService = sessionService;
        _openingBalanceService = openingBalanceService;
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<Party>> GetAllAsync()
    {
        var parties = await _repository.GetAllAsync();
        return parties.OrderByDescending(p => p.IsActive);
    }

    public async Task<PagedResult<Party>> GetPagedAsync(string? search, string? partyType, bool? isActive, int page, int pageSize)
    {
        var query = _repository.Query().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p =>
                p.Name.Contains(term) ||
                (p.Phone != null && p.Phone.Contains(term)) ||
                (p.Email != null && p.Email.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(partyType) && partyType != "All")
            query = query.Where(p => p.PartyType == partyType);

        if (isActive.HasValue)
            query = query.Where(p => p.IsActive == isActive.Value);

        int totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(p => p.IsActive)
            .ThenBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<Party>
        {
            Items = items,
            TotalCount = totalCount,
            CurrentPage = page,
            PageSize = pageSize
        };
    }

    public async Task<Party?> GetByIdAsync(int id)
    {
        return await _repository.GetByIdAsync(id);
    }

    public async Task<Party> CreateAsync(Party party, int userId)
    {
        // The ledger account, the party row and the opening-balance voucher must all land or none
        // of them: a party whose opening balance never reached the GL is exactly the sub-ledger /
        // trial-balance mismatch this posting exists to prevent.
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var created = await CreateCoreAsync(party, userId);
            await _unitOfWork.CommitTransactionAsync();
            return created;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    /// <summary>
    /// The only party types the system understands. Anything else falls through both branches of
    /// the account-creation logic below, producing a party with NO ledger account: it appears in
    /// the pickers, every posting against it fails at voucher time, and any opening balance it
    /// carries never reaches the general ledger.
    /// </summary>
    private static readonly string[] KnownPartyTypes = { "Customer", "Supplier", "Both" };

    private static string NormalizePartyType(string? partyType)
    {
        var match = KnownPartyTypes.FirstOrDefault(
            t => t.Equals(partyType?.Trim(), StringComparison.OrdinalIgnoreCase));

        if (match == null)
        {
            throw new InvalidOperationException(
                $"'{partyType}' is not a valid party type. Use one of: {string.Join(", ", KnownPartyTypes)}.");
        }

        return match;
    }

    /// <summary>
    /// Which side of the chart of accounts a party's ledger account sits on. Supplier accounts are
    /// created under payables, customer and "Both" accounts under receivables.
    /// </summary>
    private static bool IsPayableSide(string partyType)
        => partyType.Equals("Supplier", StringComparison.OrdinalIgnoreCase);

    private async Task<Party> CreateCoreAsync(Party party, int userId)
    {
        party.PartyType = NormalizePartyType(party.PartyType);
        party.CreatedAt = AppTime.Now;
        party.CreatedBy = userId;
        party.IsActive = true;

        // Automate Account Creation — resolve the receivable/payable head, subhead and type by
        // well-known code WITHIN the current pharmacy. The chart of accounts is per-tenant, so
        // ids differ between pharmacies; codes are stable and seeded at provisioning.
        var isSupplier = party.PartyType.Equals("Supplier", StringComparison.OrdinalIgnoreCase);
        var isCustomer = party.PartyType.Equals("Customer", StringComparison.OrdinalIgnoreCase)
                         || party.PartyType.Equals("Both", StringComparison.OrdinalIgnoreCase);

        if (isSupplier || isCustomer)
        {
            var headCode = isSupplier ? SupplierHeadCode : CustomerHeadCode;
            var subheadCode = isSupplier ? SupplierSubheadCode : CustomerSubheadCode;
            var typeCode = isSupplier ? SupplierTypeCode : CustomerTypeCode;

            var head = await _accountHeadRepository.Query().FirstOrDefaultAsync(h => h.Code == headCode);
            var subhead = await _accountSubheadRepository.Query().FirstOrDefaultAsync(s => s.Code == subheadCode);
            var accountType = await _accountTypeRepository.Query().FirstOrDefaultAsync(t => t.Code == typeCode);

            if (head == null || subhead == null || accountType == null)
            {
                throw new InvalidOperationException(
                    $"This pharmacy's chart of accounts is not fully set up (missing {(isSupplier ? "payable" : "receivable")} " +
                    "head/subhead/type). Contact your administrator to complete pharmacy setup.");
            }

            var account = new PharmaCare.Domain.Entities.Accounting.Account
            {
                Name = party.Name,
                AccountHead_ID = head.AccountHeadID,
                AccountSubhead_ID = subhead.AccountSubheadID,
                AccountType_ID = accountType.AccountTypeID,
                IsSystemAccount = false,
                IsActive = true,
                CreatedAt = AppTime.Now,
                CreatedBy = userId
            };

            await _accountRepository.AddAsync(account);
            await _unitOfWork.SaveChangesAsync();

            // Link the account back to the party (Pharmacy_ID is stamped automatically on save).
            party.Account_ID = account.AccountID;
        }

        await _repository.AddAsync(party);
        await _unitOfWork.SaveChangesAsync();

        // Bring any opening balance onto the books (no-op when zero).
        await _openingBalanceService.PostOpeningBalanceDeltaAsync(party, previousBalance: 0m, userId);
        await _unitOfWork.SaveChangesAsync();

        var userName = _sessionService.GetCurrentUser()?.FullName ?? "Unknown User";
        await _activityLogService.LogActivityAsync(
            userId,
            userName,
            ActivityType.Create,
            "Party",
            party.PartyID.ToString(),
            null,
            null,
            $"Created {party.PartyType}: {party.Name}");

        return party;
    }

    public async Task<bool> UpdateAsync(Party party, int userId)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var result = await UpdateCoreAsync(party, userId);
            await _unitOfWork.CommitTransactionAsync();
            return result;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    private async Task<bool> UpdateCoreAsync(Party party, int userId)
    {
        // Read the stored opening balance straight from the database, NOT from the entity loaded
        // below: if the caller happens to hold and mutate the tracked instance, that instance
        // already carries the new value and the delta would silently compute to zero, losing the
        // ledger entry. A projection is never served from the change tracker.
        // PartyType is read here for the same reason and by the same means as the opening balance.
        var storedState = await _repository.Query()
            .AsNoTracking()
            .Where(p => p.PartyID == party.PartyID)
            .Select(p => new { p.OpeningBalance, p.PartyType })
            .FirstOrDefaultAsync();

        var previousOpeningBalance = storedState?.OpeningBalance ?? 0m;

        var existing = await _repository.Query()
            .Include(p => p.Account)
            .FirstOrDefaultAsync(p => p.PartyID == party.PartyID);
        if (existing == null)
            return false;

        var newPartyType = NormalizePartyType(party.PartyType);

        // The ledger account was created under receivables or payables according to the type the
        // party had at the time, and it does not move. Letting the type cross that divide leaves a
        // "supplier" whose balance lives in an asset account: payables reports never see the money
        // owed, and the balance sheet reports a liability as an asset.
        var storedPartyType = storedState?.PartyType ?? existing.PartyType;

        if (IsPayableSide(storedPartyType) != IsPayableSide(newPartyType))
        {
            throw new InvalidOperationException(
                $"'{existing.Name}' cannot be changed from {storedPartyType} to {newPartyType}: " +
                $"its ledger account sits under {(IsPayableSide(storedPartyType) ? "payables" : "receivables")} " +
                "and its history stays there. Create a separate party for the other role.");
        }

        // Sync account name if party name changed
        if (existing.Account != null && existing.Name != party.Name)
        {
            existing.Account.Name = party.Name;
            existing.Account.UpdatedAt = AppTime.Now;
            existing.Account.UpdatedBy = userId;
        }

        existing.Name = party.Name;
        existing.PartyType = newPartyType;
        existing.Phone = party.Phone;
        existing.Email = party.Email;
        existing.Address = party.Address;
        existing.ContactNumber = party.ContactNumber;
        existing.AccountNumber = party.AccountNumber;
        existing.IBAN = party.IBAN;
        existing.OpeningBalance = party.OpeningBalance;
        existing.CreditLimit = party.CreditLimit;
        existing.IsActive = party.IsActive;
        existing.UpdatedAt = AppTime.Now;
        existing.UpdatedBy = userId;

        _repository.Update(existing);
        await _unitOfWork.SaveChangesAsync();

        // Post only the CHANGE, so editing an unrelated field posts nothing and repeated edits
        // never double up the balance.
        await _openingBalanceService.PostOpeningBalanceDeltaAsync(existing, previousOpeningBalance, userId);
        await _unitOfWork.SaveChangesAsync();

        var userName = _sessionService.GetCurrentUser()?.FullName ?? "Unknown User";
        await _activityLogService.LogActivityAsync(
            userId,
            userName,
            ActivityType.Update,
            "Party",
            existing.PartyID.ToString(),
            null,
            null,
            $"Updated {existing.PartyType}: {existing.Name}");

        return true;
    }

    public async Task<bool> ToggleStatusAsync(int id, int userId)
    {
        var party = await GetByIdAsync(id);
        if (party == null)
            return false;

        party.IsActive = !party.IsActive;
        party.UpdatedAt = AppTime.Now;
        party.UpdatedBy = userId;

        _repository.Update(party);
        await _unitOfWork.SaveChangesAsync();
        
        var userName = _sessionService.GetCurrentUser()?.FullName ?? "Unknown User";
        await _activityLogService.LogActivityAsync(
            userId,
            userName,
            ActivityType.StatusChange,
            "Party",
            party.PartyID.ToString(),
            null,
            null,
            $"{ (party.IsActive ? "Activated" : "Deactivated") } {party.PartyType}: {party.Name}");

        return true;
    }

    public async Task<Party?> GetByIdWithAccountAsync(int id)
    {
        return await _repository.Query()
            .Include(p => p.Account)
            .FirstOrDefaultAsync(p => p.PartyID == id);
    }

}
