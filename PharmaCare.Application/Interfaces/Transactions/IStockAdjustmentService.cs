using PharmaCare.Application.DTOs;
using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.Application.Interfaces.Transactions;

/// <summary>
/// Service for stock adjustments — write-offs (damaged/lost/expired) and write-ins (bonus/opening correction).
/// Uses the unified StockMain model with TransactionType code "ADJ".
/// </summary>
public interface IStockAdjustmentService
{
    Task<PagedResult<StockMain>> GetPagedAsync(string? type = null, DateTime? from = null, DateTime? to = null, int page = 1, int pageSize = 10);
    Task<StockMain?> GetByIdAsync(int id);

    /// <summary>
    /// Creates a stock adjustment (write-off or write-in).
    /// Write-off: DR Stock Loss Expense, CR Inventory
    /// Write-in:  DR Inventory, CR Adjustment Income
    /// </summary>
    Task<StockMain> CreateAsync(StockMain adjustment, int userId);

    /// <summary>Voids an adjustment and reverses the accounting entry and stock movement.</summary>
    Task<bool> VoidAsync(int id, string reason, int userId);
}
