using PharmaCare.Domain.Models.Configuration;

namespace PharmaCare.Application.Interfaces.Configuration;

public interface ISubCategoryService
{
    Task<List<SubCategory>> GetAllSubCategories();
    Task<List<SubCategory>> GetSubCategoriesByCategoryId(int categoryId);
    Task<SubCategory?> GetSubCategoryById(int id);
    Task<bool> CreateSubCategory(SubCategory subCategory);
    Task<bool> UpdateSubCategory(SubCategory subCategory);
    Task<bool> DeleteSubCategory(int id);
}
