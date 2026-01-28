using PharmaCare.Application.Implementations.SaleManagement;
using PharmaCare.Domain.Models.Inventory;

namespace PharmaCare.Application.Interfaces.SaleManagement;

public interface ISalesReturnService
{
    /// <summary>
    /// Create a sales return with inventory restock and accounting reversal
    /// </summary>
    Task<int> CreateReturn(CreateSalesReturnRequest request, int userId);

    /// <summary>
    /// Get return by ID with all details
    /// </summary>
    Task<StockMain?> GetReturnById(int id);

    /// <summary>
    /// Get all returns for a specific sale
    /// </summary>
    Task<List<StockMain>> GetReturnsBySale(int saleId);

    /// <summary>
    /// Get all returns with optional filters
    /// </summary>
    Task<List<StockMain>> GetReturns(DateTime? startDate = null, DateTime? endDate = null, int? storeId = null);

    /// <summary>
    /// Cancel a pending return
    /// </summary>
    Task<bool> CancelReturn(int returnId, int userId);

    /// <summary>
    /// Get sale details for return (with returnable quantities)
    /// </summary>
    Task<StockMain?> GetSaleForReturn(int saleId);
}
