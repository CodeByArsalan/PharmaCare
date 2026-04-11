using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCare.Application.Interfaces.Reports;
using PharmaCare.Application.ViewModels.Report;

namespace PharmaCare.Web.Controllers.Report;

[Authorize]
public class ReportController : BaseController
{
    public ReportController()
    {
    }

    /// <summary>
    /// Reports Dashboard / Index page.
    /// </summary>
    public IActionResult Index()
    {
        return View();
    }
}
