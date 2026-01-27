using PharmaCare.Application.DTOs.Finance;

namespace PharmaCare.Application.Interfaces.Finance;

public interface IFinanceService
{
    // ========== DASHBOARD/REPORTS ==========
    Task<FinanceDashboardDto> GetFinanceDashboard();
}
