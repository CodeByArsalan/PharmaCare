using PharmaCare.Application.ViewModels.Report;

namespace PharmaCare.Application.Interfaces.Reports;

public interface IPurchaseReportService
{
    Task<PurchaseReportVM> GetPurchaseReportAsync(DateRangeFilter filter);
    Task<PurchaseBySupplierVM> GetPurchaseBySupplierAsync(DateRangeFilter filter);
}
