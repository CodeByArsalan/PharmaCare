using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Domain.Entities.Configuration;

namespace PharmaCare.Web.Controllers.Configuration;

public class StoreController : Controller
{
    private readonly IStoreService _storeService;

    public StoreController(IStoreService storeService)
    {
        _storeService = storeService;
    }

    /// <summary>
    /// Gets current user ID (placeholder - integrate with Identity)
    /// </summary>
    private int GetCurrentUserId() => 1; // TODO: Get from claims

    public async Task<IActionResult> StoresIndex()
    {
        var stores = await _storeService.GetAllAsync();
        return View("StoresIndex", stores);
    }
    public IActionResult AddStore()
    {
        return View(new Store());
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddStore(Store store)
    {
        if (ModelState.IsValid)
        {
            await _storeService.CreateAsync(store, GetCurrentUserId());
            TempData["Success"] = "Store created successfully!";
            return RedirectToAction("StoresIndex");
        }
        return View(store);
    }
    public async Task<IActionResult> EditStore(int id)
    {
        var store = await _storeService.GetByIdAsync(id);
        if (store == null)
        {
            return NotFound();
        }
        return View(store);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditStore(int id, Store store)
    {
        if (id != store.StoreID)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            var updated = await _storeService.UpdateAsync(store, GetCurrentUserId());
            if (!updated)
            {
                return NotFound();
            }
            
            TempData["Success"] = "Store updated successfully!";
            return RedirectToAction("StoresIndex");
        }
        return View(store);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _storeService.ToggleStatusAsync(id, GetCurrentUserId());
        TempData["Success"] = "Store status updated successfully!";
        return RedirectToAction("StoresIndex");
    }
}
