using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.Application.Interfaces.Transactions;

/// <summary>
/// Service interface for Purchase Return operations.
/// </summary>
public interface IPurchaseReturnService
{
    /// <summary>
    /// Gets all purchase returns.
    /// </summary>
    Task<IEnumerable<StockMain>> GetAllAsync();

    /// <summary>
    /// Gets a purchase return with its details.
    /// </summary>
    Task<StockMain?> GetByIdAsync(int id);

    /// <summary>
    /// Creates a new purchase return.
    /// </summary>
    Task<StockMain> CreateAsync(StockMain purchaseReturn, int userId);

    /// <summary>
    /// Gets GRNs available for return for a specific supplier.
    /// </summary>
    Task<IEnumerable<StockMain>> GetGrnsForReturnAsync(int? supplierId = null);

    /// <summary>
    /// Voids a purchase return.
    /// </summary>
    Task<bool> VoidAsync(int id, string reason, int userId);
}
