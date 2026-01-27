using PharmaCare.Application.DTOs.POS;
using PharmaCare.Domain.Models.SaleManagement;

namespace PharmaCare.Application.Interfaces.SaleManagement;

public interface IHeldSaleService
{
    /// <summary>
    /// Hold/park current cart for later retrieval
    /// </summary>
    Task<int> HoldSale(HeldSale heldSale, int userId);

    /// <summary>
    /// Get all held sales for a store
    /// </summary>
    Task<List<HeldSale>> GetHeldSales(int storeId);

    /// <summary>
    /// Get held sale by ID with lines
    /// </summary>
    Task<HeldSale?> GetHeldSaleById(int id);

    /// <summary>
    /// Resume held sale - returns cart data
    /// </summary>
    Task<List<CartItemDto>> ResumeHeldSale(int heldSaleId);

    /// <summary>
    /// Delete held sale
    /// </summary>
    Task<bool> DeleteHeldSale(int heldSaleId, int userId);

    /// <summary>
    /// Generate hold number
    /// </summary>
    Task<string> GenerateHoldNumber();

    /// <summary>
    /// Clean up expired held sales
    /// </summary>
    Task<int> CleanupExpiredHolds();
}
