using PharmaCare.Application.DTOs;
using PharmaCare.Application.DTOs.Configuration;
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
    Task<IEnumerable<Product>> GetFilteredProductsAsync(int? categoryId, int? subCategoryId, bool? isActive, string? searchTerm);

    /// <summary>
    /// Server-side paged + filtered products for the index grid.
    /// </summary>
    Task<PagedResult<Product>> GetPagedFilteredProductsAsync(int? categoryId, int? subCategoryId, bool? isActive, string? searchTerm, int page, int pageSize);
    /// <summary>
    /// Toggles the active status of a product
    /// </summary>
    Task<bool> ToggleStatusAsync(int id, int userId);
    Task<IEnumerable<SubCategory>> GetSubCategoriesForDropdownAsync();
    Task<IEnumerable<Category>> GetCategoriesForDropdownAsync();
    Task<IEnumerable<SubCategory>> GetSubCategoriesByCategoryIdAsync(int categoryId);
    
    // Price Management
    Task<IEnumerable<PriceType>> GetPriceTypesAsync();
    Task<IEnumerable<ProductPrice>> GetProductPricesAsync(int productId);
    Task SaveProductPricesAsync(int productId, List<ProductPriceDto> prices, int userId);

    /// <summary>
    /// Full effective-dated sale-price change history for a product (newest first), for audit display.
    /// </summary>
    Task<IEnumerable<ProductPriceHistory>> GetPriceHistoryAsync(int productId);
    
    /// <summary>
    /// Gets products with calculated current stock based on transactions.
    /// Optionally filters prices by priceTypeId.
    /// </summary>
    Task<IEnumerable<(Product Product, decimal CurrentStock, decimal? SpecificPrice)>> GetProductsWithStockAsync(int? priceTypeId = null);

    /// <summary>
    /// efficient stock retrieval for validation
    /// </summary>
    Task<Dictionary<int, decimal>> GetStockStatusAsync(List<int> productIds);
    
    /// <summary>
    /// Gets the most recent GRN cost price for each product. Falls back to OpeningPrice if no GRN exists.
    /// </summary>
    Task<Dictionary<int, decimal>> GetLastGrnCostPricesAsync();
}
