using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PharmaCare.Web.Controllers;

/// <summary>
/// Home controller for dashboard.
/// Inherits from BaseController but the filter skips Home controller by default.
/// </summary>
public class HomeController : BaseController
{
    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Error()
    {
        return View();
    }
}

