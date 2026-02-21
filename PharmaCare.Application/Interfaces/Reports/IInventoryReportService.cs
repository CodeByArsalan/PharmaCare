using PharmaCare.Application.ViewModels.Report;

namespace PharmaCare.Application.Interfaces.Reports;

public interface IInventoryReportService
{
    Task<CurrentStockReportVM> GetCurrentStockReportAsync(DateRangeFilter filter);
    Task<LowStockReportVM> GetLowStockReportAsync(DateRangeFilter filter);
    Task<ProductMovementReportVM> GetProductMovementReportAsync(DateRangeFilter filter);
    Task<DeadStockReportVM> GetDeadStockReportAsync(DateRangeFilter filter);
}
