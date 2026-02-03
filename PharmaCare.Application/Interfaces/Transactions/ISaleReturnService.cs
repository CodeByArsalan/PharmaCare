using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.Application.Interfaces.Transactions;

/// <summary>
/// Service interface for Sale Return operations.
/// </summary>
public interface ISaleReturnService
{
    /// <summary>
    /// Gets all sale returns.
    /// </summary>
    Task<IEnumerable<StockMain>> GetAllAsync();

    /// <summary>
    /// Gets a sale return with its details.
    /// </summary>
    Task<StockMain?> GetByIdAsync(int id);

    /// <summary>
    /// Creates a new sale return.
    /// </summary>
    Task<StockMain> CreateAsync(StockMain saleReturn, int userId);

    /// <summary>
    /// Gets sales available for return for a specific customer.
    /// </summary>
    Task<IEnumerable<StockMain>> GetSalesForReturnAsync(int? customerId = null);

    /// <summary>
    /// Voids a sale return.
    /// </summary>
    Task<bool> VoidAsync(int id, string reason, int userId);
}
