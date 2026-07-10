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
    Task<IEnumerable<StockMain>> GetPendingGrnsAsync(int? supplierId = null, bool includePaid = false);

    Task<PharmaCare.Application.DTOs.PagedResult<StockMain>> GetPagedPendingGrnsAsync(
        int? supplierId, 
        DateTime? fromDate, 
        DateTime? toDate, 
        string? status, 
        int page, 
        int pageSize);

    /// <summary>
    /// Creates a new payment and updates transaction balance.
    /// </summary>
    Task<Payment> CreatePaymentAsync(Payment payment, int userId);

    /// <summary>
    /// Gets amount currently receivable from supplier (supplier owes company).
    /// </summary>
    Task<decimal> GetSupplierPayableToMeAsync(int supplierId);

    /// <summary>


    /// <summary>
    /// Gets all payments for a specific party/supplier.
    /// </summary>
    Task<IEnumerable<Payment>> GetPaymentsByPartyAsync(int partyId);

    /// <summary>
    /// Voids a supplier payment and reverses its accounting entries.
    /// </summary>
    Task<bool> VoidPaymentAsync(int paymentId, string reason, int userId);

    /// <summary>
    /// Refunds a supplier advance (money returned by the supplier). Bounded by the supplier's
    /// available on-account advance. Posts a REFUND payment and a cash/bank receipt voucher.
    /// </summary>
    Task<Payment> CreateSupplierRefundAsync(Payment payment, int userId);
}
