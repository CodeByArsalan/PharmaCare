using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Domain.Entities.Configuration;

namespace PharmaCare.Application.Implementations.Configuration;

/// <summary>
/// Service implementation for SubCategory entity operations
/// </summary>
public class SubCategoryService : ISubCategoryService
{
    private readonly IRepository<SubCategory> _repository;
    private readonly IRepository<Category> _categoryRepository;
    private readonly IRepository<Product> _productRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SubCategoryService(
        IRepository<SubCategory> repository,
        IRepository<Category> categoryRepository,
        IRepository<Product> productRepository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _categoryRepository = categoryRepository;
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<SubCategory>> GetAllAsync()
    {
        return await _repository.Query()
            .Include(s => s.Category)
            .OrderByDescending(s => s.IsActive)
            .ToListAsync();
    }

    public async Task<SubCategory?> GetByIdAsync(int id)
    {
        return await _repository.FirstOrDefaultAsync(s => s.SubCategoryID == id);
    }

    public async Task<SubCategory> CreateAsync(SubCategory subCategory, int userId)
    {
        await EnsureCategoryIsOursAsync(subCategory.Category_ID);
        await EnsureNameIsFreeAsync(subCategory.Name, subCategory.Category_ID, excludeId: null);

        subCategory.CreatedAt = AppTime.Now;
        subCategory.CreatedBy = userId;
        subCategory.IsActive = true;

        await _repository.AddAsync(subCategory);
        await _unitOfWork.SaveChangesAsync();

        return subCategory;
    }

    public async Task<bool> UpdateAsync(SubCategory subCategory, int userId)
    {
        var existing = await GetByIdAsync(subCategory.SubCategoryID);
        if (existing == null)
            return false;

        // Stored values as a projection, never off `existing` — a caller passing the tracked
        // instance it already mutated makes the two sides of any comparison one object.
        var stored = await _repository.Query()
            .AsNoTracking()
            .Where(s => s.SubCategoryID == subCategory.SubCategoryID)
            .Select(s => new { s.Category_ID, s.IsActive })
            .FirstOrDefaultAsync();
        if (stored == null)
            return false;

        if (subCategory.Category_ID != stored.Category_ID)
        {
            await EnsureCategoryIsOursAsync(subCategory.Category_ID);

            // A product's own Category_ID is what decides which stock/sales/COGS accounts it
            // posts to, and it is frozen once the product trades. Moving the sub-category out
            // from under its products makes the two classifications disagree — the product's
            // category and its sub-category's category give different answers, and filtering the
            // catalogue by category then sub-category returns nothing.
            var productsUsing = await _productRepository.Query()
                .CountAsync(p => p.SubCategory_ID == subCategory.SubCategoryID);

            if (productsUsing > 0)
            {
                throw new InvalidOperationException(
                    $"{productsUsing} product(s) are filed under this sub-category. Move them " +
                    "first, or create a new sub-category under the target category.");
            }
        }

        await EnsureNameIsFreeAsync(subCategory.Name, subCategory.Category_ID, subCategory.SubCategoryID);

        // Deactivating via the edit form is the toggle in different clothes; same guard.
        if (stored.IsActive && !subCategory.IsActive)
        {
            await EnsureNoActiveProductsAsync(subCategory.SubCategoryID);
        }

        existing.Name = subCategory.Name;
        existing.Category_ID = subCategory.Category_ID;
        existing.IsActive = subCategory.IsActive;
        existing.UpdatedAt = AppTime.Now;
        existing.UpdatedBy = userId;

        _repository.Update(existing);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }

    public async Task<bool> ToggleStatusAsync(int id, int userId)
    {
        var subCategory = await GetByIdAsync(id);
        if (subCategory == null)
            return false;

        if (subCategory.IsActive)
        {
            await EnsureNoActiveProductsAsync(id);
        }

        subCategory.IsActive = !subCategory.IsActive;
        subCategory.UpdatedAt = AppTime.Now;
        subCategory.UpdatedBy = userId;

        _repository.Update(subCategory);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }

    public async Task<IEnumerable<Category>> GetCategoriesForDropdownAsync()
    {
        return await _categoryRepository.Query()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    /// <summary>
    /// The category id arrives from a form. The FK carries no Pharmacy_ID, so the database will
    /// happily parent this pharmacy's sub-category under ANOTHER pharmacy's category — the tenant
    /// filter then hides the parent, and the row renders with a blank category it can never be
    /// re-assigned from.
    /// </summary>
    private async Task EnsureCategoryIsOursAsync(int categoryId)
    {
        var visible = await _categoryRepository.Query().AnyAsync(c => c.CategoryID == categoryId);
        if (!visible)
        {
            throw new InvalidOperationException("The selected category was not found.");
        }
    }

    /// <summary>
    /// Deactivating a sub-category that ACTIVE products still use leaves them sellable but
    /// un-editable: the edit form's dropdown filters on IsActive, so their own sub-category can no
    /// longer be re-selected and the next save silently reassigns or blanks it.
    /// </summary>
    private async Task EnsureNoActiveProductsAsync(int subCategoryId)
    {
        var activeProducts = await _productRepository.Query()
            .CountAsync(p => p.SubCategory_ID == subCategoryId && p.IsActive);

        if (activeProducts > 0)
        {
            throw new InvalidOperationException(
                $"{activeProducts} active product(s) still belong to this sub-category. " +
                "Move or deactivate them first.");
        }
    }

    /// <summary>Readable duplicate-name error; the unique index is the backstop.</summary>
    private async Task EnsureNameIsFreeAsync(string name, int categoryId, int? excludeId)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            throw new InvalidOperationException("A sub-category name is required.");
        }

        var taken = await _repository.Query().AnyAsync(s =>
            s.Category_ID == categoryId
            && s.Name == trimmed
            && (!excludeId.HasValue || s.SubCategoryID != excludeId.Value));

        if (taken)
        {
            throw new InvalidOperationException(
                $"A sub-category named '{trimmed}' already exists in this category.");
        }
    }
}
