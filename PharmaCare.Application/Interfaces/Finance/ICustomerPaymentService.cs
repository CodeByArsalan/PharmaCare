using PharmaCare.Domain.Entities.Finance;
using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.Application.Interfaces.Finance;

/// <summary>
/// Service for managing customer receipts/payments.
/// </summary>
public interface ICustomerPaymentService
{
    /// <summary>
    /// Gets all customer receipts.
    /// </summary>
    Task<IEnumerable<Payment>> GetAllCustomerReceiptsAsync();

    /// <summary>
    /// Gets a receipt by ID.
    /// </summary>
    Task<Payment?> GetByIdAsync(int id);

    /// <summary>
    /// Gets all receipts for a specific transaction.
    /// </summary>
    Task<IEnumerable<Payment>> GetReceiptsByTransactionAsync(int stockMainId);

    /// <summary>
    /// Gets sales with outstanding balance for receipt.
    /// </summary>
    Task<IEnumerable<StockMain>> GetPendingSalesAsync(int? customerId = null);

    /// <summary>
    /// Creates a customer receipt against a sale.
    /// </summary>
    Task<Payment> CreateReceiptAsync(Payment payment, int userId);

    /// <summary>
    /// Creates a receipt for walking customer transactions.
    /// Uses the configured walking customer account.
    /// </summary>
    Task<Payment> CreateWalkingCustomerReceiptAsync(Payment payment, int userId);

    /// <summary>
    /// Creates a refund payment to a customer (reverse of receipt).
    /// DR: Customer Account (A/R) — increases what they can claim
    /// CR: Cash/Bank Account — cash goes out
    /// </summary>
    Task<Payment> CreateRefundAsync(Payment payment, int userId);

    /// <summary>
    /// Gets all customer refunds.
    /// </summary>
    Task<IEnumerable<Payment>> GetAllRefundsAsync();
}
