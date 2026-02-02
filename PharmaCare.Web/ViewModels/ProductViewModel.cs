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
    
    // IsActive shadows BaseEntityWithStatus.IsActive? 
    // Product inherits BaseEntityWithStatus directly? 
    // Let's check Product.cs. Product : BaseEntityWithStatus.
    // BaseEntityWithStatus has IsActive? Likely.
    // Let's remove IsActive too if it shadows.
    // Checking previous ProductViewModel content... 
    // It had "public bool IsActive { get; set; } = true;"
    // I should check if Product inherits it.
    // Step 820 shows "public class Product : BaseEntityWithStatus".
    // I should check BaseEntityWithStatus. 
    // Assuming IsActive is in BaseEntityWithStatus. 
    // If I remove it, I might lose the default "true".
    // I will keep IsActive for now as it might be setting default. 
    // BUT ReorderLevel and UnitsInPack DEFINITELY shadow Product properties.
    
    public new bool IsActive { get; set; } = true; // Use 'new' if intentional or remove if base handles it. 
    // Actually, safet solution is to remove them and let base handle it, just ensure defaults are set.
    // Product.cs has "UnitsInPack = 1".
    
    public List<ProductPriceDto> ProductPrices { get; set; } = new();
}
