using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Domain.Models.Configuration;
using PharmaCare.Infrastructure.Interfaces;

namespace PharmaCare.Application.Implementations.Configuration;

public class SubCategoryService(
    IRepository<SubCategory> _subCategoryRepository,
    IRepository<Category> _categoryRepository) : ISubCategoryService
{
    public async Task<List<SubCategory>> GetAllSubCategories()
    {
        return (_subCategoryRepository.GetAllWithInclude(s => s.Category)).OrderBy(sc => sc.SubCategoryName).ToList();
    }

    public async Task<List<SubCategory>> GetSubCategoriesByCategoryId(int categoryId)
    {
        var all = await _subCategoryRepository.GetAll();
        return all.Where(sc => sc.Category_ID == categoryId).OrderBy(sc => sc.SubCategoryName).ToList();
    }

    public async Task<SubCategory?> GetSubCategoryById(int id)
    {
        return _subCategoryRepository.GetById(id);
    }

    public async Task<bool> CreateSubCategory(SubCategory subCategory)
    {
        subCategory.IsActive = true;
        subCategory.CreatedDate = DateTime.Now;

        return await _subCategoryRepository.Insert(subCategory);
    }

    public async Task<bool> UpdateSubCategory(SubCategory subCategory)
    {
        var existing = _subCategoryRepository.GetById(subCategory.SubCategoryID);
        if (existing == null) return false;

        existing.UpdatedBy = subCategory.UpdatedBy;
        existing.UpdatedDate = DateTime.Now;
        existing.SubCategoryName = subCategory.SubCategoryName;
        existing.Category_ID = subCategory.Category_ID;

        return await _subCategoryRepository.Update(existing, null);
    }

    public async Task<bool> DeleteSubCategory(int id)
    {
        var existing = _subCategoryRepository.GetById(id);
        if (existing == null) return false;

        existing.IsActive = !existing.IsActive; // Toggle status
        existing.UpdatedDate = DateTime.Now;
        return await _subCategoryRepository.Update(existing, null);
    }
}
