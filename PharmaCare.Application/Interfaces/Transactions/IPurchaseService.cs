using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.Application.Interfaces.Transactions;

/// <summary>
/// Service interface for Purchase/GRN operations.
/// </summary>
public interface IPurchaseService
{
    /// <summary>
    /// Gets all purchases/GRNs.
    /// </summary>
    Task<IEnumerable<StockMain>> GetAllAsync();

    /// <summary>
    /// Gets a purchase/GRN with its details.
    /// </summary>
    Task<StockMain?> GetByIdAsync(int id);

    /// <summary>
    /// Creates a new purchase/GRN.
    /// </summary>
    /// <param name="purchase">The purchase entity</param>
    /// <param name="userId">The user creating the purchase</param>
    /// <param name="paymentAccountId">Optional: Cash/Bank account ID for payment (if PaidAmount > 0)</param>
    Task<StockMain> CreateAsync(StockMain purchase, int userId, int? paymentAccountId = null, decimal transferredAdvanceAmount = 0);

    /// <summary>
    /// Gets approved purchase orders available for GRN.
    /// </summary>
    Task<IEnumerable<StockMain>> GetPurchaseOrdersForGrnAsync(int? supplierId = null);

    /// <summary>
    /// Voids a purchase/GRN.
    /// </summary>
    Task<bool> VoidAsync(int id, string reason, int userId);
}
