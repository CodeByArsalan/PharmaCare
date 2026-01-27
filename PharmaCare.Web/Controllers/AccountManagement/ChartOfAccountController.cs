using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaCare.Application.Interfaces.AccountManagement;
using PharmaCare.Domain.Models.AccountManagement;

namespace PharmaCare.Web.Controllers.AccountManagement;

[Authorize]
public class ChartOfAccountController : BaseController
{
    private readonly IAccountingService _accountingService;
    private readonly IHeadService _headService;
    private readonly ISubheadService _subheadService;

    public ChartOfAccountController(
        IAccountingService accountingService,
        IHeadService headService,
        ISubheadService subheadService)
    {
        _accountingService = accountingService;
        _headService = headService;
        _subheadService = subheadService;
    }
    public async Task<IActionResult> ChartOfAccountIndex()
    {
        var accounts = await _accountingService.GetChartOfAccounts();
        return View(accounts);
    }
    public async Task<IActionResult> AddChartOfAccount()
    {
        ViewBag.Heads = new SelectList(await _headService.GetHeads(), "HeadID", "HeadName");
        // FETCH Account Types from DB
        var accountTypes = await _accountingService.GetAccountTypes();
        ViewBag.AccountTypes = new SelectList(accountTypes, "AccountTypeID", "TypeName");

        return View();
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddChartOfAccount(ChartOfAccount account)
    {
        if (ModelState.IsValid)
        {
            var result = await _accountingService.CreateAccount(account, LoginUserID);
            if (result)
            {
                ShowMessage(MessageBox.Success, "Account created successfully");
                return RedirectToAction(nameof(ChartOfAccountIndex));
            }
            ModelState.AddModelError("", "Creation failed");
        }
        ViewBag.Heads = new SelectList(await _headService.GetHeads(), "HeadID", "HeadName", account.Head_ID);
        var accountTypes = await _accountingService.GetAccountTypes();
        ViewBag.AccountTypes = new SelectList(accountTypes, "AccountTypeID", "TypeName", account.AccountType_ID);
        return View(account);
    }
    public async Task<IActionResult> EditChartOfAccount(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        var decryptedId = DecryptId(id);
        var account = await _accountingService.GetAccountById(decryptedId);
        if (account == null) return NotFound();

        ViewBag.Heads = new SelectList(await _headService.GetHeads(), "HeadID", "HeadName", account.Head_ID);
        ViewBag.Subheads = new SelectList(await _subheadService.GetSubheadsByHeadId(account.Head_ID), "SubheadID", "SubheadName", account.Subhead_ID);

        var accountTypes = await _accountingService.GetAccountTypes();
        ViewBag.AccountTypes = new SelectList(accountTypes, "AccountTypeID", "TypeName", account.AccountType_ID);

        return View(account);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditChartOfAccount(ChartOfAccount account)
    {
        if (ModelState.IsValid)
        {
            var result = await _accountingService.UpdateAccount(account, LoginUserID);
            if (result)
            {
                ShowMessage(MessageBox.Success, "Account updated successfully");
                return RedirectToAction(nameof(ChartOfAccountIndex));
            }
            ModelState.AddModelError("", "Update failed");
        }
        ViewBag.Heads = new SelectList(await _headService.GetHeads(), "HeadID", "HeadName", account.Head_ID);
        ViewBag.Subheads = new SelectList(await _subheadService.GetSubheadsByHeadId(account.Head_ID), "SubheadID", "SubheadName", account.Subhead_ID);

        var accountTypes = await _accountingService.GetAccountTypes();
        ViewBag.AccountTypes = new SelectList(accountTypes, "AccountTypeID", "TypeName", account.AccountType_ID);

        return View(account);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteChartOfAccount(string id)
    {
        var decryptedId = DecryptId(id);
        var result = await _accountingService.DeactivateAccount(decryptedId, LoginUserID);
        if (result)
        {
            ShowMessage(MessageBox.Warning, "Account deleted successfully");
        }
        else
        {
            ShowMessage(MessageBox.Error, "Cannot delete account with existing transactions");
        }
        return RedirectToAction(nameof(ChartOfAccountIndex));
    }
    [HttpGet]
    public async Task<IActionResult> GetAccounts(bool activeOnly = true)
    {
        var accounts = await _accountingService.GetChartOfAccounts(activeOnly);
        return Json(accounts.Select(a => new { id = a.AccountID, name = a.AccountName, type = a.AccountType }));
    }
}
