using PharmaCare.Application.DTOs.Finance;
using PharmaCare.Domain.Models.Finance;

namespace PharmaCare.Application.Interfaces.Finance;

public interface ISupplierPaymentService
{
    // CRUD Operations
    Task<List<SupplierPayment>> GetAllPayments(int? supplierId = null, string? status = null);
    Task<SupplierPayment?> GetPaymentById(int id);
    Task<List<SupplierPayment>> GetPaymentsByGrn(int grnId);
    Task<string> GeneratePaymentNumber();

    // Supplier Outstanding
    Task<List<GrnOutstandingDto>> GetOutstandingGrns(int? supplierId = null);
    Task<decimal> GetTotalOutstandingForSupplier(int supplierId);

    // Payment Processing
    Task<bool> CreatePayment(SupplierPayment payment, int loginUserId);
    Task<bool> CancelPayment(int paymentId, int userId);

    // Reports
    Task<SupplierPaymentSummaryDto> GetPaymentSummary(int? storeId = null);
}
