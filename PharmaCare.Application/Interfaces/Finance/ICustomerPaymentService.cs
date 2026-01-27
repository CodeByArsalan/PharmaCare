using PharmaCare.Application.DTOs.Finance;

namespace PharmaCare.Application.Interfaces.Finance;

/// <summary>
/// Service for managing customer payment collection (receivables)
/// </summary>
public interface ICustomerPaymentService
{
    /// <summary>
    /// Get all customer payments with optional filtering
    /// </summary>
    Task<List<CustomerPaymentListDto>> GetAllPayments(int? customerId = null, DateTime? startDate = null, DateTime? endDate = null);

    /// <summary>
    /// Get payment by ID
    /// </summary>
    Task<CustomerPaymentListDto?> GetPaymentById(int paymentId);

    /// <summary>
    /// Get outstanding sales for a customer or all customers
    /// </summary>
    Task<List<OutstandingSaleDto>> GetOutstandingSales(int? customerId = null);

    /// <summary>
    /// Get total outstanding amount for a specific customer
    /// </summary>
    Task<decimal> GetTotalOutstandingForCustomer(int customerId);

    /// <summary>
    /// Get list of customers with outstanding balances
    /// </summary>
    Task<List<CustomerOutstandingDto>> GetCustomersWithOutstanding();

    /// <summary>
    /// Create a new customer payment
    /// </summary>
    Task<int> CreatePayment(CreateCustomerPaymentDto dto, int userId);

    /// <summary>
    /// Cancel a customer payment
    /// </summary>
    Task<bool> CancelPayment(int paymentId, string reason, int userId);
}
