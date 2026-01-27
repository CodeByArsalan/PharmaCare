using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Domain.Models.Configuration;
using PharmaCare.Infrastructure.Interfaces;

namespace PharmaCare.Application.Implementations.Configuration;

public class CategoryService(IRepository<Category> _categoryRepository) : ICategoryService
{
    public async Task<List<Category>> GetAllCategories()
    {
        return _categoryRepository.GetAllWithInclude(a => a.SaleAccount, a => a.StockAccount, a => a.COGSAccount, a => a.DamageExpenseAccount).OrderBy(c => c.CategoryName).ToList();
    }

    public async Task<Category?> GetCategoryById(int id)
    {
        return _categoryRepository.GetById(id);
    }

    public async Task<bool> CreateCategory(Category category)
    {
        category.IsActive = true;
        category.CreatedDate = DateTime.Now;

        return await _categoryRepository.Insert(category);
    }

    public async Task<bool> UpdateCategory(Category category)
    {
        var existing = _categoryRepository.GetById(category.CategoryID);
        if (existing == null) return false;

        existing.UpdatedBy = category.UpdatedBy;
        existing.UpdatedDate = DateTime.Now;
        existing.CategoryName = category.CategoryName;
        existing.SaleAccount_ID = category.SaleAccount_ID;
        existing.StockAccount_ID = category.StockAccount_ID;
        existing.COGSAccount_ID = category.COGSAccount_ID;
        existing.DamageExpenseAccount_ID = category.DamageExpenseAccount_ID;

        return await _categoryRepository.Update(existing, null);
    }

    public async Task<bool> DeleteCategory(int id)
    {
        var existing = _categoryRepository.GetById(id);
        if (existing == null) return false;

        existing.IsActive = !existing.IsActive; // Toggle status
        existing.UpdatedDate = DateTime.Now;
        return await _categoryRepository.Update(existing, null);
    }
}
