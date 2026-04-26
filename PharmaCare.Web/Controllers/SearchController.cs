using Microsoft.AspNetCore.Mvc;
using PharmaCare.Application.Interfaces.Reports;
using PharmaCare.Web.Controllers;

namespace PharmaCare.Web.Controllers;

public class SearchController : BaseController
{
    private readonly IFinancialReportService _reportService;

    public SearchController(IFinancialReportService reportService)
    {
        _reportService = reportService;
    }

    public async Task<IActionResult> Index(string q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return RedirectToAction("Index", "Home");

        var results = await _reportService.GlobalSearchAsync(q);
        return View("SearchResults", results);
    }
}
