using PharmaCare.Domain.Models.SaleManagement;

namespace PharmaCare.Application.Interfaces.SaleManagement;

public interface ISalesReturnService
{
    /// <summary>
    /// Create a sales return with inventory restock and accounting reversal
    /// </summary>
    Task<int> CreateReturn(SalesReturn salesReturn, int userId);

    /// <summary>
    /// Get return by ID with all details
    /// </summary>
    Task<SalesReturn?> GetReturnById(int id);

    /// <summary>
    /// Get all returns for a specific sale
    /// </summary>
    Task<List<SalesReturn>> GetReturnsBySale(int saleId);

    /// <summary>
    /// Get all returns with optional filters
    /// </summary>
    Task<List<SalesReturn>> GetReturns(DateTime? startDate = null, DateTime? endDate = null, int? storeId = null);

    /// <summary>
    /// Cancel a pending return
    /// </summary>
    Task<bool> CancelReturn(int returnId, int userId);

    /// <summary>
    /// Generate return number
    /// </summary>
    Task<string> GenerateReturnNumber();

    /// <summary>
    /// Get sale details for return (with returnable quantities)
    /// </summary>
    Task<Sale?> GetSaleForReturn(int saleId);
}
