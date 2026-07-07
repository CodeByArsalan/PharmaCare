using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCare.Application.Interfaces.Tenancy;

namespace PharmaCare.Web.Controllers;

/// <summary>
/// Platform super-admin area: create and manage pharmacies (tenants). This controller does NOT
/// inherit BaseController, so the per-pharmacy page-permission filter does not apply; access is
/// gated by the "PlatformAdmin" policy (the IsPlatformAdmin claim) instead.
/// </summary>
[Authorize(Policy = "PlatformAdmin")]
public class PharmaciesController : Controller
{
    private readonly IPharmacyService _pharmacyService;
    private readonly ITenantProvisioningService _provisioningService;

    public PharmaciesController(IPharmacyService pharmacyService, ITenantProvisioningService provisioningService)
    {
        _pharmacyService = pharmacyService;
        _provisioningService = provisioningService;
    }

    private int CurrentUserId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public async Task<IActionResult> Index()
    {
        var pharmacies = await _pharmacyService.GetAllAsync();
        return View(pharmacies);
    }

    [HttpGet]
    public IActionResult Create() => View(new ProvisionPharmacyRequest());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProvisionPharmacyRequest model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (await _pharmacyService.CodeExistsAsync(model.PharmacyCode?.Trim() ?? string.Empty))
        {
            ModelState.AddModelError(nameof(model.PharmacyCode), "A pharmacy with this code already exists.");
            return View(model);
        }

        var result = await _provisioningService.ProvisionAsync(model, CurrentUserId);
        if (result.Success)
        {
            TempData["Success"] = $"Pharmacy '{model.PharmacyName}' created with its admin user.";
            return RedirectToAction(nameof(Index));
        }

        ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Provisioning failed.");
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Suspend(int id)
    {
        await _pharmacyService.SetStatusAsync(id, "Suspended", CurrentUserId);
        TempData["Success"] = "Pharmacy suspended.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(int id)
    {
        await _pharmacyService.SetStatusAsync(id, "Active", CurrentUserId);
        TempData["Success"] = "Pharmacy activated.";
        return RedirectToAction(nameof(Index));
    }
}
