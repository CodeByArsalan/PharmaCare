using PharmaCare.Domain.Models.Configuration;

namespace PharmaCare.Application.Interfaces.Configuration;

public interface ICategoryService
{
    Task<List<Category>> GetAllCategories();
    Task<Category?> GetCategoryById(int id);
    Task<bool> CreateCategory(Category category);
    Task<bool> UpdateCategory(Category category);
    Task<bool> DeleteCategory(int id);
}
