using PharmaCare.Domain.Models.Inventory;
using PharmaCare.Domain.Models.PurchaseManagement;
using PharmaCare.Domain.Models.Products;
using PharmaCare.Application.DTOs.POS;

namespace PharmaCare.Application.Interfaces.Inventory;

public interface IStockService
{
    // Stock Overview
    Task<List<StoreInventory>> GetStockOverview();
    Task<List<Product>> GetProducts(); // Helper for dropdowns
    Task<int> GetLowStockItemsCount(int? storeId);
    Task<PharmaCare.Application.DTOs.Inventory.InventorySummaryDto> GetInventorySummary(int? storeId = null);

    // Purchase Return
    Task<List<PurchaseReturn>> GetPurchaseReturns();
    Task<bool> CreatePurchaseReturn(PurchaseReturn purchaseReturn, int loginUserId);
    Task<PurchaseReturn> GetPurchaseReturn(int id);
    Task<bool> ApprovePurchaseReturn(int id, int loginUserId);
    Task<List<ReturnableItemDto>> GetReturnableItems(int supplierId, int storeId);
    Task<bool> ProcessSupplierRefund(int purchaseReturnId, string refundMethod, int loginUserId);

    // Batch Search
    Task<List<ProductSearchResultDto>> SearchProductBatchesAsync(string query, int? storeId);
}

public class ReturnableItemDto
{
    public int ProductBatchID { get; set; }
    public int? StockMainID { get; set; }
    public string BatchNumber { get; set; }
    public string ProductName { get; set; }
    public decimal CostPrice { get; set; }
    public decimal QuantityOnHand { get; set; }
    public DateTime ExpiryDate { get; set; }
}
