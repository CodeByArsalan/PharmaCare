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

    Task<PharmaCare.Application.DTOs.PagedResult<StockMain>> GetPagedAsync(int? partyId, DateTime? fromDate, DateTime? toDate, string? status, int page, int pageSize);

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
    /// <param name="overrideCreditLimit">
    /// When false (default) the sale is rejected with <see cref="Exceptions.CreditLimitExceededException"/>
    /// if the unpaid portion would push the customer past their credit limit. Pass true only after an
    /// authorised user has explicitly confirmed the breach — the override is recorded on the sale.
    /// </param>
    Task<StockMain> CreateAsync(StockMain sale, int userId, int? paymentAccountId = null, bool overrideCreditLimit = false);

    /// <summary>
    /// Voids a sale.
    /// </summary>
    Task<bool> VoidAsync(int id, string reason, int userId);

    /// <summary>
    /// Gets outstanding receivable summary for a customer.
    /// </summary>
    Task<(decimal OutstandingBalance, int OpenInvoices)> GetCustomerOutstandingSummaryAsync(int customerId);
}
