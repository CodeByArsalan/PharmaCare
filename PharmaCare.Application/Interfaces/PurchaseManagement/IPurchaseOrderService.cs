using PharmaCare.Domain.Models.Inventory;
using PharmaCare.Domain.Models.PurchaseManagement;

namespace PharmaCare.Application.Interfaces.PurchaseManagement;

public interface IPurchaseOrderService
{
    Task<List<PurchaseOrder>> GetPurchaseOrders();
    Task<PurchaseOrder> GetPurchaseOrderById(int id);
    Task<bool> CreatePurchaseOrder(PurchaseOrder purchaseOrder, int loginUserId);
    Task<bool> UpdatePurchaseOrder(PurchaseOrder purchaseOrder, int loginUserId);
    Task<bool> UpdatePurchaseOrderStatus(int poId, string status);
    Task<string> GeneratePurchaseOrderNumber();
    Task<int> GetPendingPurchaseOrdersCount();

    /// <summary>
    /// Updates QuantityReceived for PO items based on GRN items
    /// </summary>
    Task<bool> UpdateReceivedQuantities(int poId, Dictionary<int, decimal> productQuantities);
}

