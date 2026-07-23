using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaCare.Web.Utilities;
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Domain.Entities.Accounting;

namespace PharmaCare.Web.Controllers.Accounting;

public class AccountSubHeadController : BaseController
{
    private readonly IAccountSubHeadService _accountSubHeadService;
    private readonly ILogger<AccountSubHeadController> _logger;

    public AccountSubHeadController(IAccountSubHeadService accountSubHeadService, ILogger<AccountSubHeadController> logger)
    {
        _accountSubHeadService = accountSubHeadService;
        _logger = logger;
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
            try
            {
                await _accountSubHeadService.CreateAsync(accountSubHead);
                ShowMessage(MessageType.Success, "Account Sub-Head created successfully!");
                return RedirectToAction("AccountSubHeadsIndex");
            }
            catch (InvalidOperationException ex)
            {
                ShowMessage(MessageType.Error, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating account sub-head");
                ShowMessage(MessageType.Error, "An unexpected error occurred while saving the account sub-head.");
            }
        }
        await LoadHeadsDropdown();
        return View(accountSubHead);
    }

    public async Task<IActionResult> EditAccountSubHead(string id)
    {
        int accountSubHeadId = Utility.DecryptId(id);
        if (accountSubHeadId == 0) return NotFound();
        var accountSubHead = await _accountSubHeadService.GetByIdAsync(accountSubHeadId);
        if (accountSubHead == null)
        {
            return NotFound();
        }
        await LoadHeadsDropdown();
        return View(accountSubHead);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditAccountSubHead(string id, AccountSubhead accountSubHead)
    {
        int accountSubHeadId = Utility.DecryptId(id);
        if (accountSubHeadId != accountSubHead.AccountSubheadID) return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                var updated = await _accountSubHeadService.UpdateAsync(accountSubHead);
                if (!updated)
                {
                    return NotFound();
                }
                ShowMessage(MessageType.Success, "Account Sub-Head updated successfully!");
                return RedirectToAction("AccountSubHeadsIndex");
            }
            catch (InvalidOperationException ex)
            {
                ShowMessage(MessageType.Error, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating account sub-head {AccountSubheadId}", accountSubHead.AccountSubheadID);
                ShowMessage(MessageType.Error, "An unexpected error occurred while saving the account sub-head.");
            }
        }
        await LoadHeadsDropdown();
        return View(accountSubHead);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        int accountSubHeadId = Utility.DecryptId(id);
        if (accountSubHeadId == 0) return NotFound();

        try
        {
            await _accountSubHeadService.DeleteAsync(accountSubHeadId);
            ShowMessage(MessageType.Success, "Account Sub-Head deleted successfully!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting account sub-head {AccountSubheadId}", accountSubHeadId);
            ShowMessage(MessageType.Error, "Could not delete the account sub-head. Please try again.");
        }
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
