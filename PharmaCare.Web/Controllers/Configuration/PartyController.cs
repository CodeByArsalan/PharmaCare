using Microsoft.AspNetCore.Mvc;
using PharmaCare.Web.Utilities;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Domain.Entities.Configuration;

namespace PharmaCare.Web.Controllers.Configuration;

public class PartyController : BaseController
{
    private readonly IPartyService _partyService;

    public PartyController(IPartyService partyService)
    {
        _partyService = partyService;
    }

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
            await _partyService.CreateAsync(party, CurrentUserId);
            ShowMessage(MessageType.Success, "Party created successfully!");
            return RedirectToAction("PartiesIndex");
        }
        return View(party);
    }
    public async Task<IActionResult> EditParty(string id)
    {
        int partyId = Utility.DecryptId(id);
        if (partyId == 0) return NotFound();
        var party = await _partyService.GetByIdAsync(partyId);
        if (party == null)
        {
            return NotFound();
        }
        return View(party);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditParty(string id, Party party)
    {
        int partyId = Utility.DecryptId(id);
        if (partyId != party.PartyID) return NotFound();

        if (ModelState.IsValid)
        {
            var updated = await _partyService.UpdateAsync(party, CurrentUserId);
            if (!updated)
            {
                return NotFound();
            }
            ShowMessage(MessageType.Success, "Party updated successfully!");
            return RedirectToAction("PartiesIndex");
        }
        return View(party);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        int partyId = Utility.DecryptId(id);
        if (partyId == 0) return NotFound();

        await _partyService.ToggleStatusAsync(partyId, CurrentUserId);
        ShowMessage(MessageType.Success, "Party status updated successfully!");
        return RedirectToAction("PartiesIndex");
    }
}
