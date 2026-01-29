using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Domain.Entities.Accounting;

namespace PharmaCare.Application.Implementations.Accounting;

public class AccountHeadService : IAccountHeadService
{
    private readonly IRepository<AccountHead> _repository;
    private readonly IRepository<AccountFamily> _familyRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AccountHeadService(
        IRepository<AccountHead> repository,
        IRepository<AccountFamily> familyRepository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _familyRepository = familyRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<AccountHead>> GetAllAsync()
    {
        return await _repository.Query()
            .Include(h => h.AccountFamily)
            .OrderBy(h => h.AccountHeadID)
            .ToListAsync();
    }

    public async Task<AccountHead?> GetByIdAsync(int id)
    {
        return await _repository.Query()
            .Include(h => h.AccountFamily)
            .FirstOrDefaultAsync(h => h.AccountHeadID == id);
    }

    public async Task<AccountHead> CreateAsync(AccountHead accountHead)
    {
        await _repository.AddAsync(accountHead);
        await _unitOfWork.SaveChangesAsync();
        return accountHead;
    }

    public async Task<bool> UpdateAsync(AccountHead accountHead)
    {
        var existing = await _repository.FirstOrDefaultAsync(h => h.AccountHeadID == accountHead.AccountHeadID);
        if (existing == null) return false;

        existing.HeadName = accountHead.HeadName;
        existing.AccountFamily_ID = accountHead.AccountFamily_ID;

        _repository.Update(existing);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var existing = await _repository.FirstOrDefaultAsync(h => h.AccountHeadID == id);
        if (existing == null) return false;

        _repository.Remove(existing);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<AccountFamily>> GetFamiliesForDropdownAsync()
    {
        return await _familyRepository.Query()
            .OrderBy(f => f.AccountFamilyID) // OR Name if preferable, but ID is standard sort for families (100, 200 etc)
            .ToListAsync();
    }
}
