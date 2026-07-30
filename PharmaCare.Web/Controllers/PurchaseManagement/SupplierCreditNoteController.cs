using Microsoft.AspNetCore.Mvc;
using PharmaCare.Application.Interfaces.Finance;
using PharmaCare.Domain.Entities.Finance;
using PharmaCare.Web.Filters;
using PharmaCare.Web.Utilities;

namespace PharmaCare.Web.Controllers.PurchaseManagement;

/// <summary>
/// Manages supplier credit notes — financial credits issued by suppliers
/// to reduce what we owe them. No stock movement occurs.
/// </summary>
public class SupplierCreditNoteController : BaseController
{
    private readonly ISupplierCreditNoteService _service;

    public SupplierCreditNoteController(ISupplierCreditNoteService service)
    {
        _service = service;
    }

    public async Task<IActionResult> SupplierCreditNotesIndex(int? supplierId, DateTime? fromDate, DateTime? toDate)
    {
        var notes = await _service.GetAllAsync(supplierId, fromDate, toDate);
        ViewBag.SelectedSupplier = supplierId;
        ViewBag.FromDate = fromDate;
        ViewBag.ToDate = toDate;
        return View(notes);
    }

    public IActionResult AddSupplierCreditNote()
    {
        return View(new SupplierCreditNote { CreditDate = AppTime.Today });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSupplierCreditNote(SupplierCreditNote creditNote)
    {
        ModelState.Remove("Party");
        ModelState.Remove("SourceStockMain");
        ModelState.Remove("AdjustmentAccount");
        ModelState.Remove("Voucher");
        ModelState.Remove("CreditNoteNo");

        if (ModelState.IsValid)
        {
            try
            {
                await _service.CreateAsync(creditNote, CurrentUserId);
                ShowMessage(MessageType.Success, "Supplier credit note recorded successfully!");
                return RedirectToAction(nameof(SupplierCreditNotesIndex));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", SafeErrorMessage(ex, "AddSupplierCreditNote"));
            }
        }

        return View(creditNote);
    }

    public async Task<IActionResult> ViewSupplierCreditNote(string id)
    {
        int cnId = Utility.DecryptId(id);
        if (cnId <= 0)
        {
            ShowMessage(MessageType.Error, "Invalid credit note ID.");
            return RedirectToAction(nameof(SupplierCreditNotesIndex));
        }

        var cn = await _service.GetByIdAsync(cnId);
        if (cn == null)
        {
            ShowMessage(MessageType.Error, "Credit note not found.");
            return RedirectToAction(nameof(SupplierCreditNotesIndex));
        }

        return View(cn);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Void(string id, string voidReason)
    {
        int cnId = Utility.DecryptId(id);
        if (cnId <= 0)
        {
            ShowMessage(MessageType.Error, "Invalid credit note ID.");
            return RedirectToAction(nameof(SupplierCreditNotesIndex));
        }

        try
        {
            var result = await _service.VoidAsync(cnId, voidReason, CurrentUserId);
            ShowMessage(result ? MessageType.Success : MessageType.Warning,
                result ? "Credit note voided successfully." : "Credit note is already voided.");
        }
        catch (Exception ex)
        {
            ShowMessage(MessageType.Error, SafeErrorMessage(ex, "Void"));
        }

        return RedirectToAction(nameof(SupplierCreditNotesIndex));
    }
}
