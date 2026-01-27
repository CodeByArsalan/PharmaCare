using PharmaCare.Domain.Models.Inventory;

namespace PharmaCare.Application.Interfaces.Inventory;

public interface IStockAlertService
{
    Task GenerateLowStockAlerts();
    Task GenerateExpiringStockAlerts(int daysThreshold = 90);
    Task<List<StockAlert>> GetActiveAlerts(int? storeId = null);
    Task<List<StockAlert>> GetAlertsByType(string alertType, int? storeId = null);
    Task<bool> ResolveAlert(int alertId, int userId);
    Task<int> GetActiveAlertCount(int? storeId = null);
}
