using PharmaCare.Domain.Models.Configuration;
using PharmaCare.Domain.Models.Products;
using PharmaCare.Domain.Models.Inventory;

namespace PharmaCare.Infrastructure.Interfaces;

public interface IPosRepository
{
    // Search & Retrieval - Returns Domain entities
    Task<List<Product>> SearchProductsAsync(string query);
    Task<ProductBatch?> GetBatchDetailsAsync(int productBatchId);
    Task<decimal> GetBatchStockQuantityAsync(int productBatchId);
    Task<decimal> GetStockQuantityAsync(int productBatchId);
    Task<StockMain?> GetSaleWithDetailsAsync(int stockMainId);

    // Party (Customer/Supplier)
    Task<Party?> GetPartyByPhoneAsync(string phone);
    Task<Party?> GetPartyByIdAsync(int id);
    void AddParty(Party party);
    void UpdateParty(Party party);

    // Order Processing (These would be part of the transaction)
    void UpdateInventory(int productBatchId, decimal quantityChange, int storeId);
    void AddStockMovement(int productBatchId, decimal quantityChange, string reason, int storeId, int createdBy, string referenceId = "");

    // Commit
    Task<int> SaveChangesAsync();

    // Sales History
    Task<List<StockMain>> GetSalesHistoryAsync(DateTime? startDate, DateTime? endDate, int? storeId = null);
}
