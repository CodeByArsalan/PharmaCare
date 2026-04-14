using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Domain.Entities.Configuration;
using PharmaCare.Web.Filters;
using PharmaCare.Web.Utilities;

namespace PharmaCare.Web.Controllers.Configuration;

[Authorize]
public class ProfitSettingsController : BaseController
{
    private readonly IProfitSettingsService _profitSettingsService;
    private readonly ILogger<ProfitSettingsController> _logger;

    public ProfitSettingsController(
        IProfitSettingsService profitSettingsService,
        ILogger<ProfitSettingsController> logger)
    {
        _profitSettingsService = profitSettingsService;
        _logger = logger;
    }

    [HttpGet]
    [LinkedToPage("ProfitSettings", "Index")]
    public async Task<IActionResult> Index()
    {
        var settings = await _profitSettingsService.GetAsync();
        return View(settings);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [LinkedToPage("ProfitSettings", "Index", PermissionType = "edit")]
    public async Task<IActionResult> Index(ProfitSettings request)
    {
        if (request.RetailProfitPercent < 0 || request.RetailProfitPercent > 100)
        {
            ModelState.AddModelError(nameof(request.RetailProfitPercent), "Retail Profit % must be between 0 and 100.");
        }

        if (request.WholesaleProfitPercent < 0 || request.WholesaleProfitPercent > 100)
        {
            ModelState.AddModelError(nameof(request.WholesaleProfitPercent), "Wholesale Profit % must be between 0 and 100.");
        }

        if (!ModelState.IsValid)
        {
            return View(request);
        }

        try
        {
            await _profitSettingsService.UpdateAsync(request.RetailProfitPercent, request.WholesaleProfitPercent, CurrentUserId);
            ShowMessage(MessageType.Success, "Profit settings updated successfully!");
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update profit settings by user {UserId}.", CurrentUserId);
            ShowMessage(MessageType.Error, "An unexpected error occurred while saving settings.");
            return View(request);
        }
    }
}
