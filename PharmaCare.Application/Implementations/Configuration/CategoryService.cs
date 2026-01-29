using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces;
using PharmaCare.Domain.Entities.Configuration;
using PharmaCare.Domain.Entities.Accounting;
using PharmaCare.Application.Interfaces.Configuration;

namespace PharmaCare.Application.Implementations.Configuration;

/// <summary>
/// Service implementation for Category entity operations
/// </summary>
public class CategoryService : ICategoryService
{
    private readonly IRepository<Category> _repository;
    private readonly IRepository<Account> _accountRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CategoryService(
        IRepository<Category> repository,
        IRepository<Account> accountRepository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _accountRepository = accountRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<Category>> GetAllAsync()
    {
        return await _repository.Query()
            .Include(c => c.SaleAccount)
            .Include(c => c.StockAccount)
            .Include(c => c.COGSAccount)
            .Include(c => c.DamageAccount)
            .OrderByDescending(c => c.IsActive)
            .ThenBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<Category?> GetByIdAsync(int id)
    {
        return await _repository.FirstOrDefaultAsync(c => c.CategoryID == id);
    }

    public async Task<Category> CreateAsync(Category category, int userId)
    {
        category.CreatedAt = DateTime.Now;
        category.CreatedBy = userId;
        category.IsActive = true;

        await _repository.AddAsync(category);
        await _unitOfWork.SaveChangesAsync();
        
        return category;
    }

    public async Task<bool> UpdateAsync(Category category, int userId)
    {
        var existing = await GetByIdAsync(category.CategoryID);
        if (existing == null)
            return false;

        existing.Name = category.Name;
        existing.SaleAccount_ID = category.SaleAccount_ID;
        existing.StockAccount_ID = category.StockAccount_ID;
        existing.COGSAccount_ID = category.COGSAccount_ID;
        existing.DamageAccount_ID = category.DamageAccount_ID;
        existing.IsActive = category.IsActive;
        existing.UpdatedAt = DateTime.Now;
        existing.UpdatedBy = userId;

        _repository.Update(existing);
        await _unitOfWork.SaveChangesAsync();
        
        return true;
    }

    public async Task<bool> ToggleStatusAsync(int id, int userId)
    {
        var category = await GetByIdAsync(id);
        if (category == null)
            return false;

        category.IsActive = !category.IsActive;
        category.UpdatedAt = DateTime.Now;
        category.UpdatedBy = userId;

        _repository.Update(category);
        await _unitOfWork.SaveChangesAsync();
        
        return true;
    }

    public async Task<IEnumerable<Account>> GetAccountsForDropdownAsync()
    {
        return await _accountRepository.Query()
            .Where(a => a.IsActive)
            .OrderBy(a => a.Code)
            .ToListAsync();
    }
    public async Task<IEnumerable<Account>> GetAccountsByTypeAsync(int typeId)
    {
        return await _accountRepository.Query()
            .Where(a => a.IsActive && a.AccountType_ID == typeId)
            .OrderBy(a => a.Code)
            .ToListAsync();
    }
}
