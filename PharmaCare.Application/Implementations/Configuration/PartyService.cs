using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.DTOs;
using PharmaCare.Application.Interfaces;
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
    private readonly IUnitOfWork _unitOfWork;

    public PartyService(
        IRepository<Party> repository,
        IRepository<PharmaCare.Domain.Entities.Accounting.Account> accountRepository,
        IRepository<PharmaCare.Domain.Entities.Accounting.AccountHead> accountHeadRepository,
        IRepository<PharmaCare.Domain.Entities.Accounting.AccountSubhead> accountSubheadRepository,
        IRepository<PharmaCare.Domain.Entities.Accounting.AccountType> accountTypeRepository,
        IActivityLogService activityLogService,
        ISessionService sessionService,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _accountRepository = accountRepository;
        _accountHeadRepository = accountHeadRepository;
        _accountSubheadRepository = accountSubheadRepository;
        _accountTypeRepository = accountTypeRepository;
        _activityLogService = activityLogService;
        _sessionService = sessionService;
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
        party.CreatedAt = DateTime.Now;
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
                CreatedAt = DateTime.Now,
                CreatedBy = userId
            };

            await _accountRepository.AddAsync(account);
            await _unitOfWork.SaveChangesAsync();

            // Link the account back to the party (Pharmacy_ID is stamped automatically on save).
            party.Account_ID = account.AccountID;
        }

        await _repository.AddAsync(party);
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
        var existing = await _repository.Query()
            .Include(p => p.Account)
            .FirstOrDefaultAsync(p => p.PartyID == party.PartyID);
        if (existing == null)
            return false;

        // Sync account name if party name changed
        if (existing.Account != null && existing.Name != party.Name)
        {
            existing.Account.Name = party.Name;
            existing.Account.UpdatedAt = DateTime.Now;
            existing.Account.UpdatedBy = userId;
        }

        existing.Name = party.Name;
        existing.PartyType = party.PartyType;
        existing.Phone = party.Phone;
        existing.Email = party.Email;
        existing.Address = party.Address;
        existing.ContactNumber = party.ContactNumber;
        existing.AccountNumber = party.AccountNumber;
        existing.IBAN = party.IBAN;
        existing.OpeningBalance = party.OpeningBalance;
        existing.CreditLimit = party.CreditLimit;
        existing.IsActive = party.IsActive;
        existing.UpdatedAt = DateTime.Now;
        existing.UpdatedBy = userId;

        _repository.Update(existing);
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
        party.UpdatedAt = DateTime.Now;
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
