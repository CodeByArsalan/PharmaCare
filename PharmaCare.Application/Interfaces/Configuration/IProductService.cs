using PharmaCare.Domain.Entities.Configuration;

namespace PharmaCare.Application.Interfaces.Configuration;

/// <summary>
/// Service interface for Product entity operations
/// </summary>
public interface IProductService
{
    Task<IEnumerable<Product>> GetAllAsync();
    Task<Product?> GetByIdAsync(int id);
    Task<Product> CreateAsync(Product product, int userId);
    Task<bool> UpdateAsync(Product product, int userId);
    /// <summary>
    /// Toggles the active status of a product
    /// </summary>
    Task<bool> ToggleStatusAsync(int id, int userId);
    Task<IEnumerable<SubCategory>> GetSubCategoriesForDropdownAsync();
    Task<IEnumerable<Category>> GetCategoriesForDropdownAsync();
    Task<string> GenerateProductCodeAsync();
}
