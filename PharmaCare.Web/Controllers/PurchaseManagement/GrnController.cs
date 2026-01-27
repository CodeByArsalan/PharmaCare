using Microsoft.AspNetCore.Mvc;
using PharmaCare.Application.Interfaces.Inventory;
using PharmaCare.Domain.Models.Inventory;
using PharmaCare.Domain.Models.PurchaseManagement;
using PharmaCare.Web.Utilities;
using PharmaCare.Web.Models.Inventory;

using PharmaCare.Application.Interfaces.PurchaseManagement;

namespace PharmaCare.Web.Controllers.PurchaseManagement;

public class GrnController : BaseController
{
    private readonly IGrnService _grnService;
    private readonly IPurchaseOrderService _poService;

    public GrnController(IGrnService grnService, IPurchaseOrderService poService)
    {
        _grnService = grnService;
        _poService = poService;
    }
    public async Task<IActionResult> GrnIndex()
    {
        var viewModel = new GrnIndexViewModel
        {
            Grns = await _grnService.GetGrns(),
            Summary = await _grnService.GetGrnSummary()
        };
        return View(viewModel);
    }
    [HttpGet]
    public async Task<IActionResult> AddGrn(string poId)
    {
        var grn = new Grn
        {
            GrnNumber = "GRN-" + DateTime.Now.Ticks,
            Store_ID = LoginUserStoreID ?? 1 // Default to user's assigned store
        };

        if (!string.IsNullOrEmpty(poId))
        {
            int decryptedPoId = DecryptId(poId);
            var po = await _poService.GetPurchaseOrderById(decryptedPoId);
            if (po != null)
            {
                grn.PurchaseOrder_ID = po.PurchaseOrderID;
                grn.PurchaseOrder = po;
                grn.Party_ID = po.Party_ID; // Pre-fill supplier (party) from PO
                ViewBag.PO = po;
            }
        }
        return View(grn);
    }
    [HttpPost]
    public async Task<IActionResult> AddGrn(Grn grn)
    {
        try
        {
            if (await _grnService.CreateGrn(grn, LoginUserID))
            {
                ShowMessage(MessageBox.Success, "GRN created and Stock updated successfully.");
                return RedirectToAction(nameof(GrnIndex));
            }

            ShowMessage(MessageBox.Error, "Failed to create GRN. Please check if Batch Number is unique or data is valid.");
        }
        catch (InvalidOperationException ex)
        {
            // Specific inventory/accounting errors
            ShowMessage(MessageBox.Error, $"GRN Error: {ex.Message}");
        }
        catch (Exception ex)
        {
            // Unexpected errors
            ShowMessage(MessageBox.Error, $"Unexpected error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"GRN Creation Error: {ex}");
        }

        // Reload PO if needed for view
        if (grn.PurchaseOrder_ID.HasValue)
        {
            var po = await _poService.GetPurchaseOrderById(grn.PurchaseOrder_ID.Value);
            ViewBag.PO = po;
        }
        return View(grn);
    }
    public async Task<IActionResult> GrnDetails(string id)
    {
        int decryptedId = DecryptId(id);
        var grn = await _grnService.GetGrnById(decryptedId);
        if (grn == null) return NotFound();
        return View(grn);
    }
}
