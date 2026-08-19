using PharmaCare.Application.DTOs.Configuration;
using PharmaCare.Domain.Entities.Configuration;

namespace PharmaCare.ViewModels;

public class ProductViewModel : Product
{
    /// <summary>
    /// Helper for input: Opening Stock Boxes
    /// </summary>
    public int OpeningStockBoxes { get; set; }

    /// <summary>
    /// Helper for input: Opening Stock Units
    /// </summary>
    public int OpeningStockUnits { get; set; }
    
    // No IsActive here on purpose. Shadowing it with `new` meant the model binder filled the
    // derived property while ProductService.UpdateAsync read the inherited one — which, defaulting
    // to true, silently reactivated every product that was edited. Active status is owned solely
    // by the ToggleStatus endpoint; the inherited BaseEntityWithStatus.IsActive already defaults
    // to true for new products.

    public List<ProductPriceDto> ProductPrices { get; set; } = new();
}
