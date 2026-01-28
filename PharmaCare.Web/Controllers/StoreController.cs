using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PharmaCare.Domain.Entities.Configuration;
using PharmaCare.Infrastructure;

namespace PharmaCare.Web.Controllers;

/// <summary>
/// Store management controller
/// </summary>
[Authorize]
public class StoreController : Controller
{
    private readonly PharmaCareDBContext _context;

    public StoreController(PharmaCareDBContext context)
    {
        _context = context;
    }

    // GET: Store/Index
    public async Task<IActionResult> StoresIndex()
    {
        var stores = await _context.Stores
            .Where(s => !s.IsDeleted)
            .OrderBy(s => s.Name)
            .ToListAsync();
        return View("StoresIndex", stores);
    }

    // GET: Store/AddStore
    public IActionResult AddStore()
    {
        return View(new Store());
    }

    // POST: Store/AddStore
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddStore(Store store)
    {
        if (ModelState.IsValid)
        {
            store.CreatedAt = DateTime.Now;
            store.CreatedBy = 1; // TODO: Get from current user
            store.IsActive = true;
            store.IsDeleted = false;
            
            _context.Stores.Add(store);
            await _context.SaveChangesAsync();
            
            TempData["Success"] = "Store created successfully!";
            return RedirectToAction("Index");
        }
        return View(store);
    }

    // GET: Store/EditStore/5
    public async Task<IActionResult> EditStore(int id)
    {
        var store = await _context.Stores.FindAsync(id);
        if (store == null || store.IsDeleted)
        {
            return NotFound();
        }
        return View(store);
    }

    // POST: Store/EditStore/5
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
            try
            {
                var existing = await _context.Stores.FindAsync(id);
                if (existing == null || existing.IsDeleted)
                {
                    return NotFound();
                }

                existing.Name = store.Name;
                existing.Address = store.Address;
                existing.Phone = store.Phone;
                existing.Email = store.Email;
                existing.IsActive = store.IsActive;
                existing.UpdatedAt = DateTime.Now;
                existing.UpdatedBy = 1; // TODO: Get from current user

                await _context.SaveChangesAsync();
                TempData["Success"] = "Store updated successfully!";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await StoreExists(id))
                {
                    return NotFound();
                }
                throw;
            }
            return RedirectToAction("Index");
        }
        return View(store);
    }

    // POST: Store/Delete/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var store = await _context.Stores.FindAsync(id);
        if (store != null)
        {
            store.IsDeleted = true;
            store.DeletedAt = DateTime.Now;
            store.DeletedBy = 1; // TODO: Get from current user
            await _context.SaveChangesAsync();
            TempData["Success"] = "Store deleted successfully!";
        }
        return RedirectToAction("Index");
    }

    private async Task<bool> StoreExists(int id)
    {
        return await _context.Stores.AnyAsync(e => e.StoreID == id && !e.IsDeleted);
    }
}
