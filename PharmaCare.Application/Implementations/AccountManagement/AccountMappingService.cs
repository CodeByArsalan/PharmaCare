using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces.AccountManagement;
using PharmaCare.Domain.Models.AccountManagement;
using PharmaCare.Infrastructure.Interfaces;

namespace PharmaCare.Application.Implementations.AccountManagement;

/// <summary>
/// Service for managing account mappings
/// </summary>
public class AccountMappingService : IAccountMappingService
{
    private readonly IRepository<AccountMapping> _mappingRepo;
    private readonly IRepository<Head> _headRepo;
    private readonly IRepository<Subhead> _subheadRepo;
    private readonly IRepository<ChartOfAccount> _accountRepo;

    public AccountMappingService(
        IRepository<AccountMapping> mappingRepo,
        IRepository<Head> headRepo,
        IRepository<Subhead> subheadRepo,
        IRepository<ChartOfAccount> accountRepo)
    {
        _mappingRepo = mappingRepo;
        _headRepo = headRepo;
        _subheadRepo = subheadRepo;
        _accountRepo = accountRepo;
    }

    public async Task<List<AccountMapping>> GetMappings(string? partyType = null)
    {
        var query = _mappingRepo.FindByCondition(m => m.IsActive);

        if (!string.IsNullOrEmpty(partyType))
        {
            query = query.Where(m => m.PartyType == partyType);
        }

        return await query
            .Include(m => m.Head)
            .Include(m => m.Subhead)
            .Include(m => m.Account)
            .OrderBy(m => m.PartyType)
            .ToListAsync();
    }

    public async Task<AccountMapping?> GetMappingById(int id)
    {
        return await _mappingRepo.FindByCondition(m => m.AccountMappingID == id)
            .Include(m => m.Head)
            .Include(m => m.Subhead)
            .Include(m => m.Account)
            .FirstOrDefaultAsync();
    }

    public async Task<AccountMapping?> GetMappingByPartyType(string partyType)
    {
        return await _mappingRepo.FindByCondition(m =>
                m.PartyType == partyType &&
                m.IsActive)
            .Include(m => m.Head)
            .Include(m => m.Subhead)
            .Include(m => m.Account)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> CreateMapping(AccountMapping mapping, int loginUserId)
    {
        // Check for duplicate mapping
        var existing = await GetMappingByPartyType(mapping.PartyType);
        if (existing != null)
        {
            return false; // Already mapped
        }

        mapping.CreatedBy = loginUserId;
        mapping.CreatedDate = DateTime.Now;
        mapping.IsActive = true;

        return await _mappingRepo.Insert(mapping);
    }

    public async Task<bool> UpdateMapping(AccountMapping mapping, int loginUserId)
    {
        var existing = await GetMappingById(mapping.AccountMappingID);
        if (existing == null) return false;

        // Check if party type is changing to one that already exists
        if (existing.PartyType != mapping.PartyType)
        {
            var duplicate = await GetMappingByPartyType(mapping.PartyType);
            if (duplicate != null && duplicate.AccountMappingID != mapping.AccountMappingID)
            {
                return false; // Would create duplicate
            }
        }

        existing.PartyType = mapping.PartyType;
        existing.Head_ID = mapping.Head_ID;
        existing.Subhead_ID = mapping.Subhead_ID;
        existing.Account_ID = mapping.Account_ID;
        existing.UpdatedBy = loginUserId;
        existing.UpdatedDate = DateTime.Now;

        return await _mappingRepo.Update(existing);
    }

    public async Task<bool> DeleteMapping(int id)
    {
        var mapping = await GetMappingById(id);
        if (mapping == null) return false;

        // Soft delete
        mapping.IsActive = false;
        return await _mappingRepo.Update(mapping);
    }

    public async Task<int?> GetAccountIdForPartyType(string partyType)
    {
        var mapping = await GetMappingByPartyType(partyType);
        if (mapping == null) return null;

        // Priority: Direct Account > Account from Subhead > Account from Head
        if (mapping.Account_ID.HasValue)
        {
            return mapping.Account_ID;
        }

        if (mapping.Subhead_ID.HasValue)
        {
            // Find first account under this subhead
            var account = await _accountRepo.FindByCondition(a =>
                    a.Subhead_ID == mapping.Subhead_ID && a.IsActive)
                .FirstOrDefaultAsync();
            return account?.AccountID;
        }

        if (mapping.Head_ID.HasValue)
        {
            // Find first account under this head
            var account = await _accountRepo.FindByCondition(a =>
                    a.Head_ID == mapping.Head_ID && a.IsActive)
                .FirstOrDefaultAsync();
            return account?.AccountID;
        }

        return null;
    }

    public async Task<List<Head>> GetHeads()
    {
        return await _headRepo.FindByCondition(h => h.IsActive)
            .OrderBy(h => h.Family)
            .ThenBy(h => h.HeadName)
            .ToListAsync();
    }

    public async Task<List<Subhead>> GetSubheadsByHead(int headId)
    {
        return await _subheadRepo.FindByCondition(s => s.Head_ID == headId && s.IsActive)
            .OrderBy(s => s.SubheadName)
            .ToListAsync();
    }

    public async Task<List<ChartOfAccount>> GetAccountsBySubhead(int subheadId)
    {
        return await _accountRepo.FindByCondition(a => a.Subhead_ID == subheadId && a.IsActive)
            .OrderBy(a => a.AccountName)
            .ToListAsync();
    }
}
