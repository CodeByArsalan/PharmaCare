using PharmaCare.Application.DTOs;
using PharmaCare.Domain.Entities.Configuration;

namespace PharmaCare.ViewModels;

/// <summary>
/// ViewModel for the Product Index page with tabs (List + Add Product)
/// </summary>
public class ProductIndexViewModel
{
    /// <summary>
    /// Paged list of products for the grid
    /// </summary>
    public PagedResult<Product> Products { get; set; } = new PagedResult<Product>();

    /// <summary>
    /// ViewModel for the Add Product form
    /// </summary>
    public ProductViewModel NewProduct { get; set; } = new ProductViewModel();

    /// <summary>
    /// Active tab: "products" or "add"
    /// </summary>
    public string ActiveTab { get; set; } = "products";
}
