using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Domain.Entities.Accounting;

namespace PharmaCare.Web.Controllers.Accounting;

public class ChartOfAccountController : BaseController
{
    private readonly IAccountService _accountService;

    public ChartOfAccountController(IAccountService accountService)
    {
        _accountService = accountService;
    }

    public async Task<IActionResult> AccountsIndex()
    {
        var accounts = await _accountService.GetAllAsync();
        return View(accounts);
    }

    public async Task<IActionResult> AddAccount()
    {
        await LoadDropdowns();
        return View(new Account());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddAccount(Account account)
    {
        if (ModelState.IsValid)
        {
            await _accountService.CreateAsync(account, CurrentUserId);
            TempData["Success"] = "Account created successfully!";
            return RedirectToAction("AccountsIndex");
        }
        await LoadDropdowns();
        return View(account);
    }

    public async Task<IActionResult> EditAccount(int id)
    {
        var account = await _accountService.GetByIdAsync(id);
        if (account == null)
        {
            return NotFound();
        }
        await LoadDropdowns();
        return View(account);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditAccount(int id, Account account)
    {
        if (id != account.AccountID)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            var updated = await _accountService.UpdateAsync(account, CurrentUserId);
            if (!updated)
            {
                return NotFound();
            }
            TempData["Success"] = "Account updated successfully!";
            return RedirectToAction("AccountsIndex");
        }
        await LoadDropdowns();
        return View(account);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await _accountService.ToggleStatusAsync(id, CurrentUserId);
        TempData["Success"] = "Account status updated successfully!";
        return RedirectToAction("AccountsIndex");
    }

    private async Task LoadDropdowns()
    {
        var subHeads = await _accountService.GetSubHeadsForDropdownAsync();
        ViewBag.SubHeads = new SelectList(
            subHeads.Select(s => new { s.AccountSubheadID, Display = s.SubheadName }),
            "AccountSubheadID", "Display");

        var types = await _accountService.GetAccountTypesForDropdownAsync();
        ViewBag.AccountTypes = new SelectList(
            types.Select(t => new { t.AccountTypeID, Display = t.Name }),
            "AccountTypeID", "Display");
    }
}
