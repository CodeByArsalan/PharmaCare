using PharmaCare.Domain.Entities.Configuration;

namespace PharmaCare.Application.Interfaces.Configuration;

/// <summary>
/// Service interface for SubCategory entity operations
/// </summary>
public interface ISubCategoryService
{
    Task<IEnumerable<SubCategory>> GetAllAsync();
    Task<SubCategory?> GetByIdAsync(int id);
    Task<SubCategory> CreateAsync(SubCategory subCategory, int userId);
    Task<bool> UpdateAsync(SubCategory subCategory, int userId);
    /// <summary>
    /// Toggles the active status of a subcategory
    /// </summary>
    Task<bool> ToggleStatusAsync(int id, int userId);
    Task<IEnumerable<Category>> GetCategoriesForDropdownAsync();
}
