using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.Application.Interfaces.Transactions;

/// <summary>
/// Service interface for Purchase Order operations.
/// </summary>
public interface IPurchaseOrderService
{
    /// <summary>
    /// Gets all purchase orders.
    /// </summary>
    Task<IEnumerable<StockMain>> GetAllAsync();

    /// <summary>
    /// Gets a purchase order with its details.
    /// </summary>
    Task<StockMain?> GetByIdAsync(int id);

    /// <summary>
    /// Creates a new purchase order.
    /// </summary>
    Task<StockMain> CreateAsync(StockMain purchaseOrder, int userId);

    /// <summary>
    /// Updates an existing purchase order.
    /// </summary>
    Task<StockMain> UpdateAsync(StockMain purchaseOrder, int userId);

    /// <summary>
    /// Approves a purchase order (Draft -> Approved).
    /// </summary>
    Task<bool> ApproveAsync(int id, int userId);

    /// <summary>
    /// Toggles the active status of a purchase order.
    /// </summary>
    Task<bool> ToggleStatusAsync(int id, int userId);

    /// <summary>
    /// Gets approved purchase orders for a specific supplier.
    /// </summary>
    Task<IEnumerable<StockMain>> GetApprovedPurchaseOrdersAsync(int? supplierId = null);
}
