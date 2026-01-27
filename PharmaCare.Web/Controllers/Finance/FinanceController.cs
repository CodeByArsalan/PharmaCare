using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaCare.Application.Interfaces.Finance;
using PharmaCare.Domain.Models.Finance;
using PharmaCare.Web.Utilities;

namespace PharmaCare.Web.Controllers.Finance;

public class FinanceController(IFinanceService _financeService) : BaseController
{
    // ========== DASHBOARD ==========

    public async Task<IActionResult> Index()
    {
        var dashboard = await _financeService.GetFinanceDashboard();
        return View(dashboard);
    }
}
