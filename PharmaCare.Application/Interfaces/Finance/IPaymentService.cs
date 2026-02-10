using PharmaCare.Domain.Entities.Finance;
using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.Application.Interfaces.Finance;

/// <summary>
/// Service for managing supplier payments.
/// </summary>
public interface IPaymentService
{
    /// <summary>
    /// Gets all payments of type PAYMENT (to suppliers).
    /// </summary>
    Task<IEnumerable<Payment>> GetAllSupplierPaymentsAsync();

    /// <summary>
    /// Gets a payment by ID.
    /// </summary>
    Task<Payment?> GetByIdAsync(int id);

    /// <summary>
    /// Gets all payments for a specific transaction.
    /// </summary>
    Task<IEnumerable<Payment>> GetPaymentsByTransactionAsync(int stockMainId);

    /// <summary>
    /// Gets GRNs with outstanding balance for payment.
    /// </summary>
    Task<IEnumerable<StockMain>> GetPendingGrnsAsync(int? supplierId = null);

    /// <summary>
    /// Creates a new payment and updates transaction balance.
    /// </summary>
    Task<Payment> CreatePaymentAsync(Payment payment, int userId);

    /// <summary>
    /// Creates an advance payment to a supplier (not linked to any GRN).
    /// DR: Supplier Account (creates debit balance / reduces payable)
    /// CR: Cash/Bank Account
    /// </summary>
    Task<Payment> CreateAdvancePaymentAsync(Payment payment, int userId);

    /// <summary>
    /// Gets all advance payments (payments without a linked transaction).
    /// </summary>
    Task<IEnumerable<Payment>> GetAdvancePaymentsAsync();
}
