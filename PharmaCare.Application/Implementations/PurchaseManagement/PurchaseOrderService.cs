using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces.PurchaseManagement;
using PharmaCare.Application.Utilities;
using PharmaCare.Domain.Models.Inventory;
using PharmaCare.Domain.Models.PurchaseManagement;
using PharmaCare.Infrastructure.Interfaces;

namespace PharmaCare.Application.Implementations.PurchaseManagement;

public class PurchaseOrderService : IPurchaseOrderService
{
    private readonly IRepository<PurchaseOrder> _poRepo;
    private readonly IRepository<PurchaseOrderItem> _poItemRepo;

    public PurchaseOrderService(IRepository<PurchaseOrder> poRepo, IRepository<PurchaseOrderItem> poItemRepo)
    {
        _poRepo = poRepo;
        _poItemRepo = poItemRepo;
    }

    public async Task<List<PurchaseOrder>> GetPurchaseOrders()
    {
        return _poRepo.GetAllWithInclude(p => p.Party).OrderByDescending(p => p.OrderDate).ToList();
    }
    public async Task<PurchaseOrder> GetPurchaseOrderById(int id)
    {
        return await _poRepo.FindByCondition(p => p.PurchaseOrderID == id)
            .Include(p => p.Party)
            .Include(p => p.PurchaseOrderItems)
            .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync();
    }
    public async Task<bool> CreatePurchaseOrder(PurchaseOrder purchaseOrder, int loginUserId)
    {
        purchaseOrder.CreatedBy = loginUserId;
        purchaseOrder.CreatedDate = DateTime.Now;
        purchaseOrder.Status = "Pending";
        purchaseOrder.TotalAmount = 0; // Reset to recount
        foreach (var item in purchaseOrder.PurchaseOrderItems)
        {
            item.TotalPrice = item.Quantity * item.UnitPrice;
            purchaseOrder.TotalAmount += item.TotalPrice;
        }
        return await _poRepo.Insert(purchaseOrder);
    }
    public async Task<bool> UpdatePurchaseOrder(PurchaseOrder purchaseOrder, int loginUserId)
    {
        var existing = await GetPurchaseOrderById(purchaseOrder.PurchaseOrderID);
        if (existing == null || existing.Status != "Pending") return false;

        // Update Header
        existing.UpdatedBy = loginUserId;
        existing.OrderDate = purchaseOrder.OrderDate;
        existing.ExpectedDeliveryDate = purchaseOrder.ExpectedDeliveryDate;
        existing.Party_ID = purchaseOrder.Party_ID;
        existing.UpdatedDate = DateTime.Now;

        // Update Items (Simplest approach: Remove all existing, Add new)
        foreach (var oldItem in existing.PurchaseOrderItems.ToList())
        {
            await _poItemRepo.Delete(oldItem);
        }

        // 2. Add new items
        decimal totalAmount = 0;
        foreach (var newItem in purchaseOrder.PurchaseOrderItems)
        {
            newItem.PurchaseOrder_ID = existing.PurchaseOrderID; // Ensure link
            newItem.TotalPrice = newItem.Quantity * newItem.UnitPrice;
            totalAmount += newItem.TotalPrice;

            await _poItemRepo.Insert(newItem);
        }

        existing.TotalAmount = totalAmount;

        return await _poRepo.Update(existing);
    }

    public async Task<string> GeneratePurchaseOrderNumber()
    {
        var today = DateTime.Now.ToString("yyyyMMdd");
        var prefix = $"PO-{today}";

        var lastPo = await _poRepo.FindByCondition(p => p.PurchaseOrderNumber.StartsWith(prefix))
            .OrderByDescending(p => p.PurchaseOrderID)
            .FirstOrDefaultAsync();

        if (lastPo == null)
        {
            return $"{prefix}-0001";
        }

        var parts = lastPo.PurchaseOrderNumber.Split('-');
        // Format: PO-yyyyMMdd-XXXX
        if (parts.Length == 3 && int.TryParse(parts[2], out int seq))
        {
            return $"{prefix}-{(seq + 1):D4}";
        }

        return $"{prefix}-0001";
    }

    public async Task<bool> UpdatePurchaseOrderStatus(int poId, string status)
    {
        var po = _poRepo.GetById(poId);
        if (po == null) return false;
        po.Status = status;
        po.UpdatedDate = DateTime.Now;
        return await _poRepo.Update(po);
    }
    public async Task<int> GetPendingPurchaseOrdersCount()
    {
        return await _poRepo.FindByCondition(p => p.Status == "Pending" || p.Status == "Partially Received").CountAsync();
    }

    public async Task<bool> UpdateReceivedQuantities(int poId, Dictionary<int, decimal> productQuantities)
    {
        var po = await _poRepo.FindByCondition(p => p.PurchaseOrderID == poId)
            .Include(p => p.PurchaseOrderItems)
            .FirstOrDefaultAsync();

        if (po == null) return false;

        foreach (var item in po.PurchaseOrderItems)
        {
            if (item.Product_ID.HasValue && productQuantities.TryGetValue(item.Product_ID.Value, out var receivedQty))
            {
                item.QuantityReceived += (int)receivedQty;
                await _poItemRepo.Update(item);
            }
        }

        // Determine if fully or partially received
        var allReceived = po.PurchaseOrderItems.All(i => i.QuantityReceived >= i.Quantity);
        var anyReceived = po.PurchaseOrderItems.Any(i => i.QuantityReceived > 0);

        if (allReceived)
        {
            po.Status = "Received";
        }
        else if (anyReceived)
        {
            po.Status = "Partially Received";
        }

        po.UpdatedDate = DateTime.Now;
        return await _poRepo.Update(po);
    }
}
