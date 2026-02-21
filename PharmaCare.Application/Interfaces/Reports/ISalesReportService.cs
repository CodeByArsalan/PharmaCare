using PharmaCare.Application.ViewModels.Report;

namespace PharmaCare.Application.Interfaces.Reports;

public interface ISalesReportService
{
    Task<DailySalesSummaryVM> GetDailySalesSummaryAsync(DateTime date);
    Task<SalesReportVM> GetSalesReportAsync(DateRangeFilter filter);
    Task<SalesByProductVM> GetSalesByProductAsync(DateRangeFilter filter);
    Task<SalesByCustomerVM> GetSalesByCustomerAsync(DateRangeFilter filter);
    Task<CustomerBalanceSummaryVM> GetCustomerBalanceSummaryAsync(DateTime asOfDate);
}
