using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces.AccountManagement;
using PharmaCare.Domain.Models.AccountManagement;
using PharmaCare.Infrastructure.Interfaces;

namespace PharmaCare.Application.Implementations.AccountManagement;

public class SubheadService(
    IRepository<Subhead> _subheadRepo,
    IRepository<ChartOfAccount> _chartOfAccountRepo) : ISubheadService
{
    public async Task<List<Subhead>> GetSubheads()
    {
        return await _subheadRepo.FindByCondition(s => true)
            .Include(s => s.Head)
            .OrderBy(s => s.Head!.Family)
            .ThenBy(s => s.Head!.HeadName)
            .ThenBy(s => s.SubheadName)
            .ToListAsync();
    }

    public async Task<List<Subhead>> GetSubheadsByHeadId(int headId)
    {
        return await _subheadRepo.FindByCondition(s => s.Head_ID == headId && s.IsActive)
            .OrderBy(s => s.SubheadName)
            .ToListAsync();
    }

    public async Task<Subhead?> GetSubheadById(int id)
    {
        return await _subheadRepo.FindByCondition(s => s.SubheadID == id)
            .Include(s => s.Head)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> CreateSubhead(Subhead subhead)
    {
        return await _subheadRepo.Insert(subhead);
    }

    public async Task<bool> UpdateSubhead(Subhead subhead)
    {
        var existing = _subheadRepo.GetById(subhead.SubheadID);
        if (existing == null) return false;

        existing.Head_ID = subhead.Head_ID;
        existing.SubheadName = subhead.SubheadName;
        existing.UpdatedBy = subhead.UpdatedBy;
        existing.UpdatedDate = DateTime.Now;

        return await _subheadRepo.Update(existing);
    }

    public async Task<bool> DeleteSubhead(int id)
    {
        var existing = _subheadRepo.GetById(id);
        if (existing == null) return false;

        // Only check for active accounts when deactivating
        if (existing.IsActive)
        {
            var hasAccounts = await _chartOfAccountRepo.FindByCondition(a => a.Subhead_ID == id && a.IsActive).AnyAsync();
            if (hasAccounts) return false;
        }

        existing.IsActive = !existing.IsActive; // Toggle status
        existing.UpdatedDate = DateTime.Now;

        return await _subheadRepo.Update(existing);
    }
}
