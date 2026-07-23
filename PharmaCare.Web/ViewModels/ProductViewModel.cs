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
    
    // Shadows the base property so new products default to active on the Add form.
    public new bool IsActive { get; set; } = true;

    public List<ProductPriceDto> ProductPrices { get; set; } = new();
}
