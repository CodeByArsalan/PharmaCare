using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaCare.Web.Utilities;
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Domain.Entities.Accounting;

namespace PharmaCare.Web.Controllers.Accounting;

public class AccountHeadController : BaseController
{
    private readonly IAccountHeadService _accountHeadService;

    public AccountHeadController(IAccountHeadService accountHeadService)
    {
        _accountHeadService = accountHeadService;
    }

    public async Task<IActionResult> AccountHeadsIndex()
    {
        var heads = await _accountHeadService.GetAllAsync();
        return View(heads);
    }

    public async Task<IActionResult> AddAccountHead()
    {
        await LoadFamiliesDropdown();
        return View(new AccountHead());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddAccountHead(AccountHead accountHead)
    {
        if (ModelState.IsValid)
        {
            await _accountHeadService.CreateAsync(accountHead);
            ShowMessage(MessageType.Success, "Account Head created successfully!");
            return RedirectToAction("AccountHeadsIndex");
        }
        await LoadFamiliesDropdown();
        return View(accountHead);
    }

    public async Task<IActionResult> EditAccountHead(string id)
    {
        int accountHeadId = Utility.DecryptId(id);
        if (accountHeadId == 0) return NotFound();
        var accountHead = await _accountHeadService.GetByIdAsync(accountHeadId);
        if (accountHead == null)
        {
            return NotFound();
        }
        await LoadFamiliesDropdown();
        return View(accountHead);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditAccountHead(string id, AccountHead accountHead)
    {
        int accountHeadId = Utility.DecryptId(id);
        if (accountHeadId != accountHead.AccountHeadID) return NotFound();

        if (ModelState.IsValid)
        {
            var updated = await _accountHeadService.UpdateAsync(accountHead);
            if (!updated)
            {
                return NotFound();
            }
            ShowMessage(MessageType.Success, "Account Head updated successfully!");
            return RedirectToAction("AccountHeadsIndex");
        }
        await LoadFamiliesDropdown();
        return View(accountHead);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        int accountHeadId = Utility.DecryptId(id);
        if (accountHeadId == 0) return NotFound();

        await _accountHeadService.DeleteAsync(accountHeadId);
        ShowMessage(MessageType.Success, "Account Head deleted successfully!");
        return RedirectToAction("AccountHeadsIndex");
    }

    private async Task LoadFamiliesDropdown()
    {
        var families = await _accountHeadService.GetFamiliesForDropdownAsync();
        ViewBag.Families = new SelectList(
            families.Select(f => new { f.AccountFamilyID, Display = f.FamilyName }),
            "AccountFamilyID", "Display");
    }
}
