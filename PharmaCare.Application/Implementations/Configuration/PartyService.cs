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
    private readonly IUnitOfWork _unitOfWork;

    public PartyService(IRepository<Party> repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
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
        party.Code = await GeneratePartyCodeAsync(party.PartyType);
        party.CreatedAt = DateTime.Now;
        party.CreatedBy = userId;
        party.IsActive = true;

        await _repository.AddAsync(party);
        await _unitOfWork.SaveChangesAsync();
        
        return party;
    }

    public async Task<bool> UpdateAsync(Party party, int userId)
    {
        var existing = await GetByIdAsync(party.PartyID);
        if (existing == null)
            return false;

        existing.Name = party.Name;
        existing.PartyType = party.PartyType;
        existing.Phone = party.Phone;
        existing.Email = party.Email;
        existing.Address = party.Address;
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

    public async Task<string> GeneratePartyCodeAsync(string partyType)
    {
        var prefix = partyType == "Customer" ? "CUS" : "SUP";
        
        var lastParty = await _repository.Query()
            .Where(p => p.PartyType == partyType)
            .OrderByDescending(p => p.PartyID)
            .FirstOrDefaultAsync();

        int nextNumber = (lastParty?.PartyID ?? 0) + 1;
        return $"{prefix}-{nextNumber:D4}";
    }
}
