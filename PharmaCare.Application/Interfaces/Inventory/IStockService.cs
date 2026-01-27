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

    // Adjustments
    Task<List<StockAdjustment>> GetStockAdjustments();
    Task<StockAdjustment> GetStockAdjustmentById(int id);
    Task<bool> AdjustStock(StockAdjustment adjustment);

    // Stock Take
    Task<List<StockTake>> GetStockTakes();
    Task<StockTake> GetStockTake(int id);
    Task<StockTake> InitiateStockTake(int storeId, int loginUserId, string remarks, int? categoryId = null);

    Task<bool> UpdateStockTakeItem(int itemId, decimal physicalQty);
    Task<bool> CompleteStockTake(int stockTakeId, int loginUserId);

    // Purchase Return
    Task<List<PurchaseReturn>> GetPurchaseReturns();
    Task<bool> CreatePurchaseReturn(PurchaseReturn purchaseReturn, int loginUserId);
    Task<PurchaseReturn> GetPurchaseReturn(int id);
    Task<bool> ApprovePurchaseReturn(int id, int loginUserId);
    Task<List<ReturnableItemDto>> GetReturnableItems(int supplierId, int storeId);
    Task<bool> ProcessSupplierRefund(int purchaseReturnId, string refundMethod, int loginUserId);


    // Stock Transfer
    Task<List<StockTransfer>> GetStockTransfers();
    Task<StockTransfer> GetStockTransferById(int id);
    Task<bool> CreateStockTransfer(StockTransfer transfer, int loginUserId);
    Task<bool> ApproveStockTransfer(int transferId, int loginUserId);
    Task<List<ReturnableItemDto>> GetTransferableItems(int storeId);
    Task<List<ProductSearchResultDto>> SearchProductBatchesAsync(string query, int? storeId);
}

public class ReturnableItemDto
{
    public int ProductBatchID { get; set; }
    public int? GrnID { get; set; }
    public string BatchNumber { get; set; }
    public string ProductName { get; set; }
    public decimal CostPrice { get; set; }
    public decimal QuantityOnHand { get; set; }
    public DateTime ExpiryDate { get; set; }
}
