using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Domain.Entities.Configuration;

namespace PharmaCare.Application.Implementations.Configuration;

/// <summary>
/// Service implementation for Party entity operations
/// </summary>
public class PartyService : IPartyService
{
    private readonly IRepository<Party> _repository;
    private readonly IRepository<PharmaCare.Domain.Entities.Accounting.Account> _accountRepository;
    private readonly IUnitOfWork _unitOfWork;

    public PartyService(
        IRepository<Party> repository, 
        IRepository<PharmaCare.Domain.Entities.Accounting.Account> accountRepository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _accountRepository = accountRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<Party>> GetAllAsync()
    {
        var parties = await _repository.GetAllAsync();
        return parties.OrderByDescending(p => p.IsActive).ThenBy(p => p.Name);
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

        // Automate Account Creation
        int headId = 0;
        int subheadId = 0;
        int typeId = 0;

        if (party.PartyType.Equals("Supplier", StringComparison.OrdinalIgnoreCase))
        {
            headId = 6;
            subheadId = 5;
            typeId = 4;
        }
        else if (party.PartyType.Equals("Customer", StringComparison.OrdinalIgnoreCase) || 
                 party.PartyType.Equals("Both", StringComparison.OrdinalIgnoreCase))
        {
            headId = 1;
            subheadId = 2;
            typeId = 3;
        }

        if (headId > 0 && subheadId > 0)
        {
            var account = new PharmaCare.Domain.Entities.Accounting.Account
            {
                Name = party.Name,
                AccountHead_ID = headId,
                AccountSubhead_ID = subheadId,
                AccountType_ID = typeId,
                IsSystemAccount = false,
                IsActive = true,
                CreatedAt = DateTime.Now,
                CreatedBy = userId
            };

            await _accountRepository.AddAsync(account);
            await _unitOfWork.SaveChangesAsync();

            // Link the account back to the party
            party.Account_ID = account.AccountID;
        }

        await _repository.AddAsync(party);
        await _unitOfWork.SaveChangesAsync();
        
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
        
        return true;
    }

}
