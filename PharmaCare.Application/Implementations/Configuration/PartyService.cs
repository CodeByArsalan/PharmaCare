using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Domain.Models.AccountManagement;
using PharmaCare.Domain.Models.Configuration;
using PharmaCare.Infrastructure.Interfaces;

namespace PharmaCare.Application.Implementations.Configuration;

public class PartyService : IPartyService
{
    private readonly IRepository<Party> _partyRepo;
    private readonly IRepository<AccountMapping> _mappingRepo;
    private readonly IRepository<ChartOfAccount> _accountRepo;

    public PartyService(
        IRepository<Party> partyRepo,
        IRepository<AccountMapping> mappingRepo,
        IRepository<ChartOfAccount> accountRepo)
    {
        _partyRepo = partyRepo;
        _mappingRepo = mappingRepo;
        _accountRepo = accountRepo;
    }

    public async Task<List<Party>> GetParties()
    {
        return await _partyRepo.FindByCondition(p => true)
            .OrderBy(p => p.PartyName)
            .ToListAsync();
    }

    public async Task<List<Party>> GetPartiesByType(string partyType)
    {
        return await _partyRepo.FindByCondition(p => p.IsActive &&
            (p.PartyType == partyType || p.PartyType == "Both"))
            .OrderBy(p => p.PartyName)
            .ToListAsync();
    }

    public async Task<Party?> GetPartyById(int id)
    {
        return await _partyRepo.FindByCondition(p => p.PartyID == id)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> CreateParty(Party party, int userId)
    {
        party.IsActive = true;
        party.CreatedBy = userId;
        party.CreatedDate = DateTime.Now;

        var result = await _partyRepo.Insert(party);

        if (result)
        {
            // Auto-create ChartOfAccount entry based on AccountMapping
            await CreateChartOfAccountForParty(party, userId);
        }

        return result;
    }

    public async Task<bool> UpdateParty(Party party, int userId)
    {
        var existing = _partyRepo.GetById(party.PartyID);
        if (existing == null) return false;

        existing.PartyType = party.PartyType;
        existing.PartyName = party.PartyName;
        existing.ContactNumber = party.ContactNumber;
        existing.AccountNumber = party.AccountNumber;
        existing.IBAN = party.IBAN;
        existing.AccountAddress = party.AccountAddress;
        existing.Address = party.Address;
        existing.OpeningBalance = party.OpeningBalance;
        existing.CreditLimit = party.CreditLimit;
        existing.Market = party.Market;
        existing.UpdatedBy = userId;
        existing.UpdatedDate = DateTime.Now;

        return await _partyRepo.Update(existing);
    }

    public async Task<bool> DeleteParty(int id)
    {
        var existing = _partyRepo.GetById(id);
        if (existing == null) return false;

        existing.IsActive = !existing.IsActive; // Toggle status
        existing.UpdatedDate = DateTime.Now;

        return await _partyRepo.Update(existing);
    }

    public async Task<List<Party>> GetCustomers()
    {
        return await _partyRepo.FindByCondition(p => p.IsActive &&
            (p.PartyType == "Customer" || p.PartyType == "Both"))
            .OrderBy(p => p.PartyName)
            .ToListAsync();
    }

    public async Task<List<Party>> GetSuppliers()
    {
        return await _partyRepo.FindByCondition(p => p.IsActive &&
            (p.PartyType == "Supplier" || p.PartyType == "Both"))
            .OrderBy(p => p.PartyName)
            .ToListAsync();
    }

    #region Private Helpers

    /// <summary>
    /// Creates a ChartOfAccount entry for a newly created party
    /// Uses Head_ID and Subhead_ID from AccountMapping table based on PartyType
    /// </summary>
    private async Task CreateChartOfAccountForParty(Party party, int userId)
    {
        try
        {
            // Find the AccountMapping for this PartyType
            var mapping = await _mappingRepo.FindByCondition(m =>
                    m.PartyType == party.PartyType && m.IsActive)
                .FirstOrDefaultAsync();

            // If no exact match, try "Both" mapping as fallback
            if (mapping == null && party.PartyType != "Both")
            {
                mapping = await _mappingRepo.FindByCondition(m =>
                        m.PartyType == "Both" && m.IsActive)
                    .FirstOrDefaultAsync();
            }

            // If no mapping exists, skip account creation
            if (mapping == null)
            {
                System.Diagnostics.Debug.WriteLine($"No AccountMapping found for PartyType '{party.PartyType}'. Skipping ChartOfAccount creation.");
                return;
            }

            // Determine AccountType_ID based on PartyType
            // Customer = 3, Supplier = 4, Both = 3
            int accountTypeId = party.PartyType switch
            {
                "Customer" => 3,
                "Supplier" => 4,
                "Both" => 3,
                _ => 3 // Default to Customer account type
            };

            // Create the ChartOfAccount entry
            var chartOfAccount = new ChartOfAccount
            {
                AccountName = party.PartyName,
                Head_ID = mapping.Head_ID ?? 0,
                Subhead_ID = mapping.Subhead_ID ?? 0,
                AccountType_ID = accountTypeId,
                AccountAddress = party.AccountAddress,
                IBAN = party.IBAN,
                AccountNo = party.AccountNumber,
                IsActive = true,
                CreatedBy = userId,
                CreatedDate = DateTime.Now
            };

            await _accountRepo.Insert(chartOfAccount);
        }
        catch (Exception ex)
        {
            // Log error but don't fail party creation
            System.Diagnostics.Debug.WriteLine($"Error creating ChartOfAccount for party {party.PartyName}: {ex.Message}");
        }
    }

    #endregion
}
