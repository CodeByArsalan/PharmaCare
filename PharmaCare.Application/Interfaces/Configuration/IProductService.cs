using PharmaCare.Domain.Models.Configuration;
using PharmaCare.Domain.Models.Products;

namespace PharmaCare.Application.Interfaces.Configuration;

public interface IProductService
{
    Task<List<Product>> GetProducts();
    Task<Product?> GetProductById(int productId);
    Task<bool> CreateProduct(Product product);
    Task<bool> UpdateProduct(Product product);
    Task<bool> DeleteProduct(int productId);
    Task<List<SubCategory>> GetSubCategories();
}
