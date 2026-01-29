using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Domain.Entities.Accounting;

namespace PharmaCare.Application.Implementations.Accounting;

public class AccountSubHeadService : IAccountSubHeadService
{
    private readonly IRepository<AccountSubhead> _repository;
    private readonly IRepository<AccountHead> _headRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AccountSubHeadService(
        IRepository<AccountSubhead> repository,
        IRepository<AccountHead> headRepository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _headRepository = headRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<AccountSubhead>> GetAllAsync()
    {
        return await _repository.Query()
            .Include(s => s.AccountHead)
            .OrderBy(s => s.AccountSubheadID)
            .ToListAsync();
    }

    public async Task<AccountSubhead?> GetByIdAsync(int id)
    {
        return await _repository.Query()
            .Include(s => s.AccountHead)
            .FirstOrDefaultAsync(s => s.AccountSubheadID == id);
    }

    public async Task<AccountSubhead> CreateAsync(AccountSubhead accountSubhead)
    {
        await _repository.AddAsync(accountSubhead);
        await _unitOfWork.SaveChangesAsync();
        return accountSubhead;
    }

    public async Task<bool> UpdateAsync(AccountSubhead accountSubhead)
    {
        var existing = await _repository.FirstOrDefaultAsync(s => s.AccountSubheadID == accountSubhead.AccountSubheadID);
        if (existing == null) return false;

        existing.SubheadName = accountSubhead.SubheadName;
        existing.AccountHead_ID = accountSubhead.AccountHead_ID;

        _repository.Update(existing);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var existing = await _repository.FirstOrDefaultAsync(s => s.AccountSubheadID == id);
        if (existing == null) return false;

        _repository.Remove(existing);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<AccountHead>> GetHeadsForDropdownAsync()
    {
        return await _headRepository.Query()
            .OrderBy(h => h.HeadName)
            .ToListAsync();
    }
}
