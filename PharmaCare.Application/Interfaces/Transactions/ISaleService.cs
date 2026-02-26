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
    /// Creates a new sale with optional payment.
    /// </summary>
    /// <param name="sale">The sale entity.</param>
    /// <param name="userId">The user creating the sale.</param>
    /// <param name="paymentAccountId">Optional payment account ID for immediate payment.</param>
    Task<StockMain> CreateAsync(StockMain sale, int userId, int? paymentAccountId = null);

    /// <summary>
    /// Voids a sale.
    /// </summary>
    Task<bool> VoidAsync(int id, string reason, int userId);

    /// <summary>
    /// Gets outstanding receivable summary for a customer.
    /// </summary>
    Task<(decimal OutstandingBalance, int OpenInvoices)> GetCustomerOutstandingSummaryAsync(int customerId);
}
