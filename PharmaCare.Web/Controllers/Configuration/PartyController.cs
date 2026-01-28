using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Domain.Entities.Configuration;

namespace PharmaCare.Web.Controllers.Configuration;
public class PartyController : Controller
{
    private readonly IPartyService _partyService;

    public PartyController(IPartyService partyService)
    {
        _partyService = partyService;
    }

    private int GetCurrentUserId() => 1;
    public async Task<IActionResult> PartiesIndex()
    {
        var parties = await _partyService.GetAllAsync();
        return View("PartiesIndex", parties);
    }
    public IActionResult AddParty()
    {
        return View(new Party());
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddParty(Party party)
    {
        if (ModelState.IsValid)
        {
            await _partyService.CreateAsync(party, GetCurrentUserId());
            TempData["Success"] = "Party created successfully!";
            return RedirectToAction("PartiesIndex");
        }
        return View(party);
    }
    public async Task<IActionResult> EditParty(int id)
    {
        var party = await _partyService.GetByIdAsync(id);
        if (party == null)
        {
            return NotFound();
        }
        return View(party);
    }
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
            var updated = await _partyService.UpdateAsync(party, GetCurrentUserId());
            if (!updated)
            {
                return NotFound();
            }
            TempData["Success"] = "Party updated successfully!";
            return RedirectToAction("PartiesIndex");
        }
        return View(party);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _partyService.ToggleStatusAsync(id, GetCurrentUserId());
        TempData["Success"] = "Party status updated successfully!";
        return RedirectToAction("PartiesIndex");
    }
}
