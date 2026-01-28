using PharmaCare.Application.DTOs.Inventory;
using PharmaCare.Application.Implementations.PurchaseManagement;
using PharmaCare.Domain.Models.Inventory;

namespace PharmaCare.Application.Interfaces.PurchaseManagement;

public interface IPurchaseService
{
    Task<List<StockMain>> GetPurchases();
    Task<StockMain?> GetPurchaseById(int id);
    Task<bool> CreatePurchase(CreatePurchaseRequest request, int loginUserId);
    Task<PurchaseSummaryDto> GetPurchaseSummary();
}
