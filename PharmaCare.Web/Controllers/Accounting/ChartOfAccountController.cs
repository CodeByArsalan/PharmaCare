using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaCare.Web.Utilities;
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Domain.Entities.Accounting;

namespace PharmaCare.Web.Controllers.Accounting;

public class ChartOfAccountController : BaseController
{
    private readonly IAccountService _accountService;
    private readonly ILogger<ChartOfAccountController> _logger;

    public ChartOfAccountController(IAccountService accountService, ILogger<ChartOfAccountController> logger)
    {
        _accountService = accountService;
        _logger = logger;
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
    public async Task<IActionResult> AddAccount(
        [Bind("Name,AccountHead_ID,AccountSubhead_ID,AccountType_ID,IsSystemAccount")] Account account)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await _accountService.CreateAsync(account, CurrentUserId);
                ShowMessage(MessageType.Success, "Account created successfully!");
                return RedirectToAction("AccountsIndex");
            }
            catch (InvalidOperationException ex)
            {
                ShowMessage(MessageType.Error, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating account");
                ShowMessage(MessageType.Error, "An unexpected error occurred while saving the account.");
            }
        }
        await LoadDropdowns();
        return View(account);
    }

    public async Task<IActionResult> EditAccount(string id)
    {
        int accountId = Utility.DecryptId(id);
        if (accountId == 0) return NotFound();
        var account = await _accountService.GetByIdAsync(accountId);
        if (account == null)
        {
            return NotFound();
        }
        await LoadDropdowns();
        return View(account);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditAccount(string id,
        [Bind("AccountID,Name,AccountHead_ID,AccountSubhead_ID,AccountType_ID,IsSystemAccount,IsActive")] Account account)
    {
        int accountId = Utility.DecryptId(id);
        if (accountId != account.AccountID) return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                var updated = await _accountService.UpdateAsync(account, CurrentUserId);
                if (!updated)
                {
                    return NotFound();
                }
                ShowMessage(MessageType.Success, "Account updated successfully!");
                return RedirectToAction("AccountsIndex");
            }
            catch (InvalidOperationException ex)
            {
                ShowMessage(MessageType.Error, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating account {AccountId}", account.AccountID);
                ShowMessage(MessageType.Error, "An unexpected error occurred while saving the account.");
            }
        }
        await LoadDropdowns();
        return View(account);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        int accountId = Utility.DecryptId(id);
        if (accountId == 0) return NotFound();

        try
        {
            await _accountService.ToggleStatusAsync(accountId, CurrentUserId);
            ShowMessage(MessageType.Success, "Account status updated successfully!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling account {AccountId}", accountId);
            ShowMessage(MessageType.Error, "Could not change the account's status. Please try again.");
        }
        return RedirectToAction("AccountsIndex");
    }

    private async Task LoadDropdowns()
    {
        var subHeads = await _accountService.GetSubHeadsForDropdownAsync();
        ViewBag.SubHeads = new SelectList(
            subHeads.Select(s => new { s.AccountSubheadID, Display = s.SubheadName }),
            "AccountSubheadID", "Display");

        var heads = await _accountService.GetAccountHeadsForDropdownAsync();
        ViewBag.AccountHeads = new SelectList(
            heads.Select(h => new { h.AccountHeadID, Display = h.HeadName }),
            "AccountHeadID", "Display");

        var types = await _accountService.GetAccountTypesForDropdownAsync();
        ViewBag.AccountTypes = new SelectList(
            types.Select(t => new { t.AccountTypeID, Display = t.Name }),
            "AccountTypeID", "Display");
    }

    [HttpGet]
    public async Task<IActionResult> GetSubHeadsByHeadId(int headId)
    {
        var subHeads = await _accountService.GetSubHeadsByHeadIdAsync(headId);
        return Json(subHeads.Select(s => new { id = s.AccountSubheadID, name = s.SubheadName }));
    }
}
