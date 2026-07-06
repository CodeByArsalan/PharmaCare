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

    public async Task<IActionResult> PartiesIndex(string? search, string? partyType, int? status, int page = 1, int pageSize = 25)
    {
        pageSize = NormalizePageSize(pageSize);
        bool? isActive = status switch { 1 => true, 0 => false, _ => null };

        var result = await _partyService.GetPagedAsync(search, partyType, isActive, page, pageSize);

        ViewBag.Search = search;
        ViewBag.SelectedType = partyType ?? "All";
        ViewBag.SelectedStatus = status;

        return View("PartiesIndex", result);
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
