using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Domain.Entities.Accounting;

namespace PharmaCare.Application.Implementations.Accounting;

public class AccountService : IAccountService
{
    private readonly IRepository<Account> _repository;
    private readonly IRepository<AccountSubhead> _subheadRepository;
    private readonly IRepository<AccountHead> _headRepository;
    private readonly IRepository<AccountType> _typeRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AccountService(
        IRepository<Account> repository,
        IRepository<AccountSubhead> subheadRepository,
        IRepository<AccountHead> headRepository,
        IRepository<AccountType> typeRepository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _subheadRepository = subheadRepository;
        _headRepository = headRepository;
        _typeRepository = typeRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<Account>> GetAllAsync()
    {
        return await _repository.Query()
            .Include(a => a.AccountSubhead)
            .Include(a => a.AccountHead)
            .Include(a => a.AccountType)
            .OrderBy(a => a.Name)
            .ToListAsync();
    }

    public async Task<Account?> GetByIdAsync(int id)
    {
        return await _repository.Query()
            .Include(a => a.AccountSubhead)
            .Include(a => a.AccountHead)
            .Include(a => a.AccountType)
            .FirstOrDefaultAsync(a => a.AccountID == id);
    }

    public async Task<Account> CreateAsync(Account account, int userId)
    {
        account.CreatedAt = DateTime.Now;
        account.CreatedBy = userId;
        account.IsActive = true; 

        await _repository.AddAsync(account);
        await _unitOfWork.SaveChangesAsync();
        return account;
    }

    public async Task<bool> UpdateAsync(Account account, int userId)
    {
        var existing = await _repository.FirstOrDefaultAsync(a => a.AccountID == account.AccountID);
        if (existing == null) return false;


        existing.Name = account.Name;
        existing.AccountHead_ID = account.AccountHead_ID;
        existing.AccountSubhead_ID = account.AccountSubhead_ID;
        existing.AccountType_ID = account.AccountType_ID;
        existing.IsSystemAccount = account.IsSystemAccount;
        // Don't update IsActive here, usually separate Toggle, but standard UI might have checkbox. 
        // Based on CategoryService, UpdateAsync includes IsActive update.
        existing.IsActive = account.IsActive; 
        
        existing.UpdatedAt = DateTime.Now;
        existing.UpdatedBy = userId;

        _repository.Update(existing);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ToggleStatusAsync(int id, int userId)
    {
        var account = await _repository.FirstOrDefaultAsync(a => a.AccountID == id);
        if (account == null) return false;

        account.IsActive = !account.IsActive;
        account.UpdatedAt = DateTime.Now;
        account.UpdatedBy = userId;

        _repository.Update(account);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<AccountSubhead>> GetSubHeadsForDropdownAsync()
    {
        return await _subheadRepository.Query()
            .OrderBy(s => s.SubheadName)
            .ToListAsync();
    }

    public async Task<IEnumerable<AccountType>> GetAccountTypesForDropdownAsync()
    {
        return await _typeRepository.Query()
            .OrderBy(t => t.Name)
            .ToListAsync();
    }
    public async Task<IEnumerable<AccountHead>> GetAccountHeadsForDropdownAsync()
    {
        return await _headRepository.Query()
            .OrderBy(h => h.HeadName)
            .ToListAsync();
    }

    public async Task<IEnumerable<AccountSubhead>> GetSubHeadsByHeadIdAsync(int headId)
    {
        return await _subheadRepository.Query()
            .Where(s => s.AccountHead_ID == headId)
            .OrderBy(s => s.SubheadName)
            .ToListAsync();
    }

    public async Task<IEnumerable<Account>> GetCashBankAccountsAsync()
    {
        return await _repository.Query()
            .Include(a => a.AccountType)
            .Where(a => a.IsActive && (a.AccountType_ID == 1 || a.AccountType_ID == 2))
            .OrderBy(a => a.Name)
            .ToListAsync();
    }
}
