using Microsoft.AspNetCore.Mvc;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Domain.Entities.Configuration;

namespace PharmaCare.Web.Controllers.Configuration;

public class StoreController : BaseController
{
    private readonly IStoreService _storeService;

    public StoreController(IStoreService storeService)
    {
        _storeService = storeService;
    }


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
            await _storeService.CreateAsync(store, CurrentUserId);
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
            var updated = await _storeService.UpdateAsync(store, CurrentUserId);
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
        await _storeService.ToggleStatusAsync(id, CurrentUserId);
        TempData["Success"] = "Store status updated successfully!";
        return RedirectToAction("StoresIndex");
    }
}
