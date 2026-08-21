using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces;
using PharmaCare.Domain.Entities.Configuration;
using PharmaCare.Domain.Entities.Accounting;
using PharmaCare.Domain.Entities.Transactions;
using PharmaCare.Application.Interfaces.Configuration;

namespace PharmaCare.Application.Implementations.Configuration;

/// <summary>
/// Service implementation for Category entity operations
/// </summary>
public class CategoryService : ICategoryService
{
    private readonly IRepository<Category> _repository;
    private readonly IRepository<Account> _accountRepository;
    private readonly IRepository<StockDetail> _stockDetailRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CategoryService(
        IRepository<Category> repository,
        IRepository<Account> accountRepository,
        IRepository<StockDetail> stockDetailRepository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _accountRepository = accountRepository;
        _stockDetailRepository = stockDetailRepository;
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
            .ToListAsync();
    }

    public async Task<Category?> GetByIdAsync(int id)
    {
        return await _repository.FirstOrDefaultAsync(c => c.CategoryID == id);
    }

    public async Task<Category> CreateAsync(Category category, int userId)
    {
        await EnsureNameIsFreeAsync(category.Name, excludeId: null);
        await ValidateAccountOwnershipAsync(category);

        category.CreatedAt = AppTime.Now;
        category.CreatedBy = userId;
        category.IsActive = true;

        await _repository.AddAsync(category);
        await _unitOfWork.SaveChangesAsync();

        return category;
    }

    /// <summary>
    /// The four control-account ids are client input and every sale/adjustment voucher of this
    /// category posts to them verbatim. The FK alone accepts another pharmacy's AccountID, which
    /// would route this tenant's postings onto accounts its own reports cannot resolve — so each
    /// id must resolve through the tenant-filtered account repository.
    /// </summary>
    private async Task ValidateAccountOwnershipAsync(Category category)
    {
        var requestedIds = new[]
            {
                category.SaleAccount_ID, category.StockAccount_ID,
                category.COGSAccount_ID, category.DamageAccount_ID
            }
            .Where(id => id.HasValue && id.Value > 0)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        if (requestedIds.Count == 0)
            return;

        var known = await _accountRepository.Query()
            .Where(a => requestedIds.Contains(a.AccountID))
            .Select(a => a.AccountID)
            .ToListAsync();

        if (requestedIds.Any(id => !known.Contains(id)))
            throw new InvalidOperationException("One or more selected accounts were not found in this pharmacy's chart of accounts.");
    }

    /// <summary>
    /// Two categories with one name are two different sets of posting accounts behind a single
    /// label — which one a product lands in decides where its stock and revenue post, and the
    /// dropdown gives no way to tell them apart. Readable error here; the unique index on
    /// (Pharmacy_ID, Name) is the backstop for writes that bypass this service.
    /// </summary>
    private async Task EnsureNameIsFreeAsync(string name, int? excludeId)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            throw new InvalidOperationException("A category name is required.");
        }

        var taken = await _repository.Query().AnyAsync(c =>
            c.Name == trimmed && (!excludeId.HasValue || c.CategoryID != excludeId.Value));

        if (taken)
        {
            throw new InvalidOperationException($"A category named '{trimmed}' already exists.");
        }
    }

    public async Task<bool> UpdateAsync(Category category, int userId)
    {
        var existing = await GetByIdAsync(category.CategoryID);
        if (existing == null)
            return false;

        // These four accounts ARE the category's posting instructions. Stock already received under
        // this category was debited to the current stock account; if the account is re-pointed, the
        // eventual sale credits a different one and the original balance is stranded on the balance
        // sheet with no document that can ever clear it. Same argument for revenue, COGS and damage.
        // Read as a projection, not off `existing`: a caller that passes the tracked instance it
        // already mutated would make the two sides of this comparison the same object.
        var stored = await _repository.Query()
            .AsNoTracking()
            .Where(c => c.CategoryID == category.CategoryID)
            .Select(c => new { c.SaleAccount_ID, c.StockAccount_ID, c.COGSAccount_ID, c.DamageAccount_ID })
            .FirstOrDefaultAsync();

        if (stored == null)
            return false;

        var accountsChanged =
            stored.SaleAccount_ID != category.SaleAccount_ID ||
            stored.StockAccount_ID != category.StockAccount_ID ||
            stored.COGSAccount_ID != category.COGSAccount_ID ||
            stored.DamageAccount_ID != category.DamageAccount_ID;

        if (accountsChanged && await HasPostedMovementAsync(category.CategoryID))
        {
            throw new InvalidOperationException(
                $"The control accounts for '{existing.Name}' cannot be changed: its products have " +
                "already posted to the current accounts, and re-pointing them would strand those " +
                "balances. Create a new category for the new accounts instead.");
        }

        await EnsureNameIsFreeAsync(category.Name, category.CategoryID);
        await ValidateAccountOwnershipAsync(category);

        existing.Name = category.Name;
        existing.SaleAccount_ID = category.SaleAccount_ID;
        existing.StockAccount_ID = category.StockAccount_ID;
        existing.COGSAccount_ID = category.COGSAccount_ID;
        existing.DamageAccount_ID = category.DamageAccount_ID;
        existing.IsActive = category.IsActive;
        existing.UpdatedAt = AppTime.Now;
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
        category.UpdatedAt = AppTime.Now;
        category.UpdatedBy = userId;

        _repository.Update(category);
        await _unitOfWork.SaveChangesAsync();
        
        return true;
    }

    /// <summary>True when any product in this category has ever appeared on a stock document.</summary>
    private Task<bool> HasPostedMovementAsync(int categoryId)
        => _stockDetailRepository.Query().AsNoTracking()
            .AnyAsync(sd => sd.Product!.Category_ID == categoryId);

    public async Task<IEnumerable<Account>> GetAccountsForDropdownAsync()
    {
        return await _accountRepository.Query()
            .Where(a => a.IsActive)
            .OrderBy(a => a.Name)
            .ToListAsync();
    }
    public async Task<IEnumerable<Account>> GetAccountsByTypeAsync(int typeId)
    {
        return await _accountRepository.Query()
            .Where(a => a.IsActive && a.AccountType_ID == typeId)
            .OrderBy(a => a.Name)
            .ToListAsync();
    }
}
