using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PharmaCare.Domain.Entities.Configuration;
using PharmaCare.Infrastructure;

namespace PharmaCare.Web.Controllers;

/// <summary>
/// Party (Customer/Supplier) management controller
/// </summary>
[Authorize]
public class PartyController : Controller
{
    private readonly PharmaCareDBContext _context;

    public PartyController(PharmaCareDBContext context)
    {
        _context = context;
    }

    // GET: Party/Index
    public async Task<IActionResult> PartiesIndex()
    {
        var parties = await _context.Parties
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.PartyType)
            .ThenBy(p => p.Name)
            .ToListAsync();
        return View("PartiesIndex", parties);
    }

    // GET: Party/AddParty
    public IActionResult AddParty()
    {
        return View(new Party());
    }

    // POST: Party/AddParty
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddParty(Party party)
    {
        if (ModelState.IsValid)
        {
            // Auto-generate party code
            var prefix = party.PartyType == "Customer" ? "C" : "S";
            var lastParty = await _context.Parties
                .Where(p => p.PartyType == party.PartyType)
                .OrderByDescending(p => p.PartyID)
                .FirstOrDefaultAsync();
            int nextCode = (lastParty?.PartyID ?? 0) + 1;
            party.Code = $"{prefix}{nextCode:D5}";
            
            party.CreatedAt = DateTime.Now;
            party.CreatedBy = 1;
            party.IsActive = true;
            party.IsDeleted = false;
            
            _context.Parties.Add(party);
            await _context.SaveChangesAsync();
            
            TempData["Success"] = "Party created successfully!";
            return RedirectToAction("Index");
        }
        return View(party);
    }

    // GET: Party/EditParty/5
    public async Task<IActionResult> EditParty(int id)
    {
        var party = await _context.Parties.FindAsync(id);
        if (party == null || party.IsDeleted)
        {
            return NotFound();
        }
        return View(party);
    }

    // POST: Party/EditParty/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditParty(int id, Party party)
    {
        if (id != party.PartyID)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            var existing = await _context.Parties.FindAsync(id);
            if (existing == null || existing.IsDeleted)
            {
                return NotFound();
            }

            existing.Name = party.Name;
            existing.PartyType = party.PartyType;
            existing.Phone = party.Phone;
            existing.Email = party.Email;
            existing.Address = party.Address;
            existing.OpeningBalance = party.OpeningBalance;
            existing.CreditLimit = party.CreditLimit;
            existing.IsActive = party.IsActive;
            existing.UpdatedAt = DateTime.Now;
            existing.UpdatedBy = 1;

            await _context.SaveChangesAsync();
            TempData["Success"] = "Party updated successfully!";
            return RedirectToAction("Index");
        }
        return View(party);
    }

    // POST: Party/Delete/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var party = await _context.Parties.FindAsync(id);
        if (party != null)
        {
            party.IsDeleted = true;
            party.DeletedAt = DateTime.Now;
            party.DeletedBy = 1;
            await _context.SaveChangesAsync();
            TempData["Success"] = "Party deleted successfully!";
        }
        return RedirectToAction("Index");
    }
}
