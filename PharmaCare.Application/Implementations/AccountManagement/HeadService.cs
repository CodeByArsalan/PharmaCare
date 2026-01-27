using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces.AccountManagement;
using PharmaCare.Domain.Models.AccountManagement;
using PharmaCare.Infrastructure.Interfaces;

namespace PharmaCare.Application.Implementations.AccountManagement;

public class HeadService(
    IRepository<Head> _headRepo,
    IRepository<Subhead> _subheadRepo) : IHeadService
{
    public async Task<List<Head>> GetHeads()
    {
        return await _headRepo.FindByCondition(h => true)
            .OrderBy(h => h.Family)
            .ThenBy(h => h.HeadName)
            .ToListAsync();
    }

    public async Task<Head?> GetHeadById(int id)
    {
        return await _headRepo.FindByCondition(h => h.HeadID == id)
            .Include(h => h.Subheads.Where(s => s.IsActive))
            .FirstOrDefaultAsync();
    }

    public async Task<bool> CreateHead(Head head)
    {
        return await _headRepo.Insert(head);
    }

    public async Task<bool> UpdateHead(Head head)
    {
        var existing = _headRepo.GetById(head.HeadID);
        if (existing == null) return false;

        existing.Family = head.Family;
        existing.HeadName = head.HeadName;
        existing.UpdatedBy = head.UpdatedBy;
        existing.UpdatedDate = DateTime.Now;

        return await _headRepo.Update(existing);
    }

    public async Task<bool> DeleteHead(int id)
    {
        var existing = _headRepo.GetById(id);
        if (existing == null) return false;

        // Only check for active subheads when deactivating
        if (existing.IsActive)
        {
            var hasSubheads = await _subheadRepo.FindByCondition(s => s.Head_ID == id && s.IsActive).AnyAsync();
            if (hasSubheads) return false;
        }

        existing.IsActive = !existing.IsActive; // Toggle status
        existing.UpdatedDate = DateTime.Now;

        return await _headRepo.Update(existing);
    }
}
