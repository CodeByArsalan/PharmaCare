using PharmaCare.Domain.Models.SaleManagement;

namespace PharmaCare.Application.Interfaces.SaleManagement;

public interface IQuotationService
{
    /// <summary>
    /// Create a new quotation
    /// </summary>
    Task<int> CreateQuotation(Quotation quotation, int userId);

    /// <summary>
    /// Get quotation by ID with lines
    /// </summary>
    Task<Quotation?> GetQuotationById(int id);

    /// <summary>
    /// Get quotations with filters
    /// </summary>
    Task<List<Quotation>> GetQuotations(int? storeId = null, string? status = null);

    /// <summary>
    /// Convert quotation to sale
    /// </summary>
    Task<int> ConvertToSale(int quotationId, int userId);

    /// <summary>
    /// Update quotation
    /// </summary>
    Task<bool> UpdateQuotation(Quotation quotation, int userId);

    /// <summary>
    /// Cancel quotation
    /// </summary>
    Task<bool> CancelQuotation(int quotationId, int userId);

    /// <summary>
    /// Generate quotation number
    /// </summary>
    Task<string> GenerateQuotationNumber();

    /// <summary>
    /// Mark expired quotations
    /// </summary>
    Task<int> MarkExpiredQuotations();
}
