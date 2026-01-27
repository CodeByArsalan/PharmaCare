using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaCare.Application.Interfaces.AccountManagement;
using PharmaCare.Domain.Models.AccountManagement;

namespace PharmaCare.Web.Controllers.AccountManagement;

[Authorize]
public class AccountMappingController(IAccountMappingService _mappingService) : BaseController
{
    public async Task<IActionResult> AccountMappingIndex()
    {
        var mappings = await _mappingService.GetMappings(null);
        return View(mappings);
    }
    public async Task<IActionResult> AddMapping()
    {
        return View(new AccountMapping { PartyType = "" });
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMapping(AccountMapping mapping)
    {
        // Remove navigation property validation errors
        ModelState.Remove("Head");
        ModelState.Remove("Subhead");
        ModelState.Remove("Account");

        if (ModelState.IsValid)
        {
            var result = await _mappingService.CreateMapping(mapping, LoginUserID);
            if (result)
            {
                ShowMessage(MessageBox.Success, "Account mapping created successfully");
                return RedirectToAction(nameof(AccountMappingIndex));
            }
            ShowMessage(MessageBox.Error, "A mapping for this party type already exists");
        }
        return View(mapping);
    }
    public async Task<IActionResult> EditMapping(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        int decryptedId = DecryptId(id);
        var mapping = await _mappingService.GetMappingById(decryptedId);
        if (mapping == null) return NotFound();

        return View(mapping);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditMapping(AccountMapping mapping)
    {
        // Remove navigation property validation errors
        ModelState.Remove("Head");
        ModelState.Remove("Subhead");
        ModelState.Remove("Account");

        if (ModelState.IsValid)
        {
            var result = await _mappingService.UpdateMapping(mapping, LoginUserID);
            if (result)
            {
                ShowMessage(MessageBox.Success, "Account mapping updated successfully");
                return RedirectToAction(nameof(AccountMappingIndex));
            }
            ShowMessage(MessageBox.Error, "Update failed - a mapping for this party type may already exist");
        }

        return View(mapping);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMapping(string id)
    {
        int decryptedId = DecryptId(id);
        var result = await _mappingService.DeleteMapping(decryptedId);
        if (result)
        {
            ShowMessage(MessageBox.Warning, "Account mapping deleted");
        }
        else
        {
            ShowMessage(MessageBox.Error, "Cannot delete mapping");
        }
        return RedirectToAction(nameof(AccountMappingIndex));
    }

    #region AJAX Endpoints
    [HttpGet]
    public async Task<IActionResult> GetHeads()
    {
        var heads = await _mappingService.GetHeads();
        return Json(heads.Select(h => new { id = h.HeadID, name = h.HeadName }));
    }
    [HttpGet]
    public async Task<IActionResult> GetSubheadsByHead(int headId)
    {
        var subheads = await _mappingService.GetSubheadsByHead(headId);
        return Json(subheads.Select(s => new { id = s.SubheadID, name = s.SubheadName }));
    }
    [HttpGet]
    public async Task<IActionResult> GetAccountsBySubhead(int subheadId)
    {
        var accounts = await _mappingService.GetAccountsBySubhead(subheadId);
        return Json(accounts.Select(a => new { id = a.AccountID, name = a.AccountName }));
    }

    #endregion
}
