using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Domain.Models.Configuration;

namespace PharmaCare.Web.Controllers.Configuration;

[Authorize]
public class PartyController(IPartyService _partyService) : BaseController
{
    public async Task<IActionResult> PartyIndex(string? type = null)
    {
        var parties = string.IsNullOrEmpty(type)
            ? await _partyService.GetParties()
            : await _partyService.GetPartiesByType(type);

        ViewBag.SelectedType = type;
        return View(parties);
    }
    public IActionResult AddParty()
    {
        ViewBag.PartyTypes = new SelectList(new[] { "Customer", "Supplier", "Both" });
        return View();
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddParty(Party party)
    {
        if (ModelState.IsValid)
        {
            var result = await _partyService.CreateParty(party, LoginUserID);
            if (result)
            {
                ShowMessage(MessageBox.Success, "Party created successfully");
                return RedirectToAction(nameof(PartyIndex));
            }
            ModelState.AddModelError("", "Creation failed");
        }
        ViewBag.PartyTypes = new SelectList(new[] { "Customer", "Supplier", "Both" }, party.PartyType);
        return View(party);
    }
    public async Task<IActionResult> EditParty(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        int decryptedId = DecryptId(id);
        var party = await _partyService.GetPartyById(decryptedId);
        if (party == null) return NotFound();

        ViewBag.PartyTypes = new SelectList(new[] { "Customer", "Supplier", "Both" }, party.PartyType);
        return View(party);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditParty(Party party)
    {
        if (ModelState.IsValid)
        {
            var result = await _partyService.UpdateParty(party, LoginUserID);
            if (result)
            {
                ShowMessage(MessageBox.Success, "Party updated successfully");
                return RedirectToAction(nameof(PartyIndex));
            }
            ModelState.AddModelError("", "Update failed");
        }
        ViewBag.PartyTypes = new SelectList(new[] { "Customer", "Supplier", "Both" }, party.PartyType);
        return View(party);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteParty(string id)
    {
        int decryptedId = DecryptId(id);
        var result = await _partyService.DeleteParty(decryptedId);
        if (result)
        {
            ShowMessage(MessageBox.Warning, "Party deleted successfully");
        }
        else
        {
            ShowMessage(MessageBox.Error, "Cannot delete party");
        }
        return RedirectToAction(nameof(PartyIndex));
    }
    [HttpGet]
    public async Task<IActionResult> GetPartiesByType(string type)
    {
        var parties = await _partyService.GetPartiesByType(type);
        return Json(parties.Select(p => new { id = p.PartyID, name = p.PartyName }));
    }
}
