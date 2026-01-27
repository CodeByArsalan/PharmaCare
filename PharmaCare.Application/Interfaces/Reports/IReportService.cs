using PharmaCare.Application.DTOs.Reports;

namespace PharmaCare.Application.Interfaces.Reports;

public interface IReportService
{
    Task<SalesReportDto> GetSalesReport(DateTime startDate, DateTime endDate, int? storeId = null);
    Task<InventoryReportDto> GetInventoryReport(int? storeId = null);
    Task<List<SalesDetailDto>> GetSalesDetailReport(DateTime startDate, DateTime endDate, int? storeId = null);
    Task<List<StockMovementDto>> GetStockMovementReport(DateTime startDate, DateTime endDate, string? productName = null, int? storeId = null);

    // New Reports
    Task<List<SlowMovingItemDto>> GetSlowMovingItemsReport(int daysThreshold = 30, int? storeId = null);
    Task<PurchaseReportDto> GetPurchaseReport(DateTime startDate, DateTime endDate, int? storeId = null);
    Task<ProfitLossDto> GetProfitLossReport(DateTime startDate, DateTime endDate, int? storeId = null);
    Task<CustomerAnalyticsDto> GetCustomerAnalyticsReport(DateTime startDate, DateTime endDate, int? storeId = null);
    Task<ExpiryWastageReportDto> GetExpiryWastageReport(int? storeId = null);
}
