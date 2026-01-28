using PharmaCare.Domain.Entities.Configuration;
using PharmaCare.Domain.Entities.Accounting;

namespace PharmaCare.Application.Interfaces.Configuration;

/// <summary>
/// Service interface for Category entity operations
/// </summary>
public interface ICategoryService
{
    Task<IEnumerable<Category>> GetAllAsync();
    Task<Category?> GetByIdAsync(int id);
    Task<Category> CreateAsync(Category category, int userId);
    Task<bool> UpdateAsync(Category category, int userId);
    /// <summary>
    /// Toggles the active status of a category
    /// </summary>
    Task<bool> ToggleStatusAsync(int id, int userId);
    Task<IEnumerable<Account>> GetAccountsForDropdownAsync();
}
