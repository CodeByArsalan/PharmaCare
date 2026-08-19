using Microsoft.AspNetCore.Mvc;
using PharmaCare.Web.Utilities;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Domain.Entities.Configuration;

namespace PharmaCare.Web.Controllers.Configuration;

public class PartyController : BaseController
{
    private readonly IPartyService _partyService;
    private readonly ILogger<PartyController> _logger;

    public PartyController(IPartyService partyService, ILogger<PartyController> logger)
    {
        _partyService = partyService;
        _logger = logger;
    }

    public async Task<IActionResult> PartiesIndex(string? search, string? partyType, int? status, int page = 1, int pageSize = 25)
    {
        page = NormalizePage(page);
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
    public async Task<IActionResult> AddParty(
        [Bind("PartyType,IsWholeSale,Name,Phone,ContactNumber,Email,OpeningBalance,AccountNumber,IBAN,CreditLimit,Address")] Party party)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await _partyService.CreateAsync(party, CurrentUserId);
                ShowMessage(MessageType.Success, "Party created successfully!");
                return RedirectToAction("PartiesIndex");
            }
            catch (InvalidOperationException ex)
            {
                ShowMessage(MessageType.Error, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating party");
                ShowMessage(MessageType.Error, "An unexpected error occurred while saving the party.");
            }
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
    public async Task<IActionResult> EditParty(string id,
        [Bind("PartyID,PartyType,IsWholeSale,Name,Phone,ContactNumber,Email,OpeningBalance,AccountNumber,IBAN,CreditLimit,Address,IsActive")] Party party)
    {
        int partyId = Utility.DecryptId(id);
        if (partyId != party.PartyID) return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                var updated = await _partyService.UpdateAsync(party, CurrentUserId);
                if (!updated)
                {
                    return NotFound();
                }
                ShowMessage(MessageType.Success, "Party updated successfully!");
                return RedirectToAction("PartiesIndex");
            }
            catch (InvalidOperationException ex)
            {
                ShowMessage(MessageType.Error, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating party {PartyId}", party.PartyID);
                ShowMessage(MessageType.Error, "An unexpected error occurred while saving the party.");
            }
        }
        return View(party);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        int partyId = Utility.DecryptId(id);
        if (partyId == 0) return NotFound();

        try
        {
            await _partyService.ToggleStatusAsync(partyId, CurrentUserId);
            ShowMessage(MessageType.Success, "Party status updated successfully!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling party {PartyId}", partyId);
            ShowMessage(MessageType.Error, "Could not change the party's status. Please try again.");
        }
        return RedirectToAction("PartiesIndex");
    }
}
