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
    private readonly IUnitOfWork _unitOfWork;

    public SubCategoryService(
        IRepository<SubCategory> repository,
        IRepository<Category> categoryRepository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _categoryRepository = categoryRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<SubCategory>> GetAllAsync()
    {
        return await _repository.Query()
            .Include(s => s.Category)
            .OrderByDescending(s => s.IsActive)
            .ThenBy(s => s.Name)
            .ToListAsync();
    }

    public async Task<SubCategory?> GetByIdAsync(int id)
    {
        return await _repository.FirstOrDefaultAsync(s => s.SubCategoryID == id);
    }

    public async Task<SubCategory> CreateAsync(SubCategory subCategory, int userId)
    {
        subCategory.CreatedAt = DateTime.Now;
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

        existing.Name = subCategory.Name;
        existing.Category_ID = subCategory.Category_ID;
        existing.IsActive = subCategory.IsActive;
        existing.UpdatedAt = DateTime.Now;
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

        subCategory.IsActive = !subCategory.IsActive;
        subCategory.UpdatedAt = DateTime.Now;
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
}
