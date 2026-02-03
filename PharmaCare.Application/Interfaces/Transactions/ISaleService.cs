using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.Application.Interfaces.Transactions;

/// <summary>
/// Service interface for Sale operations.
/// </summary>
public interface ISaleService
{
    /// <summary>
    /// Gets all sales.
    /// </summary>
    Task<IEnumerable<StockMain>> GetAllAsync();

    /// <summary>
    /// Gets a sale with its details.
    /// </summary>
    Task<StockMain?> GetByIdAsync(int id);

    /// <summary>
    /// Creates a new sale.
    /// </summary>
    Task<StockMain> CreateAsync(StockMain sale, int userId);

    /// <summary>
    /// Voids a sale.
    /// </summary>
    Task<bool> VoidAsync(int id, string reason, int userId);
}
