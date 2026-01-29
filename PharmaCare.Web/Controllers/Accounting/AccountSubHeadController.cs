using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Domain.Entities.Accounting;

namespace PharmaCare.Web.Controllers.Accounting;

public class AccountSubHeadController : BaseController
{
    private readonly IAccountSubHeadService _accountSubHeadService;

    public AccountSubHeadController(IAccountSubHeadService accountSubHeadService)
    {
        _accountSubHeadService = accountSubHeadService;
    }

    public async Task<IActionResult> AccountSubHeadsIndex()
    {
        var subHeads = await _accountSubHeadService.GetAllAsync();
        return View(subHeads);
    }

    public async Task<IActionResult> AddAccountSubHead()
    {
        await LoadHeadsDropdown();
        return View(new AccountSubhead());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddAccountSubHead(AccountSubhead accountSubHead)
    {
        if (ModelState.IsValid)
        {
            await _accountSubHeadService.CreateAsync(accountSubHead);
            TempData["Success"] = "Account Sub-Head created successfully!";
            return RedirectToAction("AccountSubHeadsIndex");
        }
        await LoadHeadsDropdown();
        return View(accountSubHead);
    }

    public async Task<IActionResult> EditAccountSubHead(int id)
    {
        var accountSubHead = await _accountSubHeadService.GetByIdAsync(id);
        if (accountSubHead == null)
        {
            return NotFound();
        }
        await LoadHeadsDropdown();
        return View(accountSubHead);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditAccountSubHead(int id, AccountSubhead accountSubHead)
    {
        if (id != accountSubHead.AccountSubheadID)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            var updated = await _accountSubHeadService.UpdateAsync(accountSubHead);
            if (!updated)
            {
                return NotFound();
            }
            TempData["Success"] = "Account Sub-Head updated successfully!";
            return RedirectToAction("AccountSubHeadsIndex");
        }
        await LoadHeadsDropdown();
        return View(accountSubHead);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _accountSubHeadService.DeleteAsync(id);
        TempData["Success"] = "Account Sub-Head deleted successfully!";
        return RedirectToAction("AccountSubHeadsIndex");
    }

    private async Task LoadHeadsDropdown()
    {
        var heads = await _accountSubHeadService.GetHeadsForDropdownAsync();
        ViewBag.Heads = new SelectList(
            heads.Select(h => new { h.AccountHeadID, Display = h.HeadName }),
            "AccountHeadID", "Display");
    }
}
