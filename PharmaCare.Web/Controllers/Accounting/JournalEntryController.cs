using Microsoft.AspNetCore.Mvc;
using PharmaCare.Application.DTOs.Accounting;
using PharmaCare.Application.Interfaces.AccountManagement;
using PharmaCare.Web.Controllers;

namespace PharmaCare.Web.Controllers.Accounting;

public class JournalEntryController : BaseController
{
    private readonly IAccountingService _accountingService;

    public JournalEntryController(IAccountingService accountingService)
    {
        _accountingService = accountingService;
    }
    public async Task<IActionResult> JournalEntryIndex(DateTime? fromDate, DateTime? toDate, string? status)
    {
        // Default to current month if no dates provided
        fromDate ??= new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        toDate ??= DateTime.Now;

        var entries = await _accountingService.GetJournalEntries(fromDate, toDate, status);

        ViewBag.FromDate = fromDate.Value.ToString("yyyy-MM-dd");
        ViewBag.ToDate = toDate.Value.ToString("yyyy-MM-dd");
        ViewBag.Status = status;

        return View(entries);
    }
    public async Task<IActionResult> JournalEntryDetails(string id)
    {
        int decryptedId = DecryptId(id);
        var entry = await _accountingService.GetJournalEntryById(decryptedId);
        if (entry == null)
        {
            return NotFound();
        }
        return View(entry);
    }
    public IActionResult AddJournalEntry()
    {
        var dto = new JournalEntryDto
        {
            EntryDate = DateTime.Now,
            PostingDate = DateTime.Now,
            EntryType = "Manual"
        };
        return View(dto);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddJournalEntry(JournalEntryDto dto)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var journalEntryId = await _accountingService.CreateJournalEntry(dto, LoginUserID);
                if (journalEntryId > 0)
                {
                    ShowMessage(MessageBox.Success, "Journal entry created successfully");
                    return RedirectToAction(nameof(JournalEntryDetails), new { id = journalEntryId });
                }
            }
            catch (Exception ex)
            {
                ShowMessage(MessageBox.Error, ex.Message);
            }
        }
        return View(dto);
    }
    [HttpPost]
    public async Task<IActionResult> Post(int id)
    {
        var result = await _accountingService.PostJournalEntry(id, LoginUserID);
        if (result)
        {
            return Json(new { success = true, message = "Journal entry posted successfully" });
        }
        return Json(new { success = false, message = "Failed to post journal entry. Ensure debits equal credits." });
    }
    [HttpPost]
    public async Task<IActionResult> Void(int id)
    {
        var result = await _accountingService.VoidJournalEntry(id, LoginUserID);
        if (result)
        {
            return Json(new { success = true, message = "Journal entry voided successfully" });
        }
        return Json(new { success = false, message = "Failed to void journal entry" });
    }
    [HttpPost]
    public async Task<IActionResult> ValidateEntry([FromBody] JournalEntryDto dto)
    {
        var validation = await _accountingService.ValidateJournalEntry(dto);
        return Json(new { isValid = validation.IsValid, errors = validation.Errors });
    }
}
