using Microsoft.AspNetCore.Mvc;
using PharmaCare.Application.Interfaces.PurchaseManagement;
using PharmaCare.Application.Implementations.PurchaseManagement;
using PharmaCare.Domain.Models.Inventory;
using PharmaCare.Web.Utilities;
using PharmaCare.Web.Models.Inventory;

namespace PharmaCare.Web.Controllers.PurchaseManagement;

public class PurchaseController : BaseController
{
    private readonly IPurchaseService _purchaseService;
    private readonly IPurchaseOrderService _poService;

    public PurchaseController(IPurchaseService purchaseService, IPurchaseOrderService poService)
    {
        _purchaseService = purchaseService;
        _poService = poService;
    }

    public async Task<IActionResult> PurchaseIndex()
    {
        var viewModel = new PurchaseIndexViewModel
        {
            Purchases = await _purchaseService.GetPurchases(),
            Summary = await _purchaseService.GetPurchaseSummary()
        };
        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> AddPurchase(string poId)
    {
        var viewModel = new AddPurchaseViewModel
        {
            StoreId = LoginUserStoreID ?? 1
        };

        if (!string.IsNullOrEmpty(poId))
        {
            int decryptedPoId = DecryptId(poId);
            var po = await _poService.GetPurchaseOrderById(decryptedPoId);
            if (po != null)
            {
                viewModel.PurchaseOrderId = po.PurchaseOrderID;
                viewModel.PartyId = po.Party_ID;
                viewModel.PurchaseOrder = po;
            }
        }
        return View(viewModel);
    }

    [HttpPost]
    public async Task<IActionResult> AddPurchase(AddPurchaseViewModel viewModel)
    {
        try
        {
            var request = new CreatePurchaseRequest
            {
                StoreId = viewModel.StoreId,
                PartyId = viewModel.PartyId,
                PurchaseOrderId = viewModel.PurchaseOrderId,
                SupplierInvoiceNo = viewModel.SupplierInvoiceNo,
                Remarks = viewModel.Remarks,
                Items = viewModel.Items?.Select(i => new CreatePurchaseItemRequest
                {
                    ProductId = i.ProductId,
                    BatchNumber = i.BatchNumber,
                    ExpiryDate = i.ExpiryDate,
                    Quantity = i.Quantity,
                    CostPrice = i.CostPrice,
                    SellingPrice = i.SellingPrice
                }).ToList() ?? new()
            };

            if (await _purchaseService.CreatePurchase(request, LoginUserID))
            {
                ShowMessage(MessageBox.Success, "Purchase created and Stock updated successfully.");
                return RedirectToAction(nameof(PurchaseIndex));
            }

            ShowMessage(MessageBox.Error, "Failed to create Purchase. Please check if Batch Number is unique or data is valid.");
        }
        catch (InvalidOperationException ex)
        {
            ShowMessage(MessageBox.Error, $"Purchase Error: {ex.Message}");
        }
        catch (Exception ex)
        {
            ShowMessage(MessageBox.Error, $"Unexpected error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Purchase Creation Error: {ex}");
        }

        // Reload PO if needed for view
        if (viewModel.PurchaseOrderId.HasValue)
        {
            viewModel.PurchaseOrder = await _poService.GetPurchaseOrderById(viewModel.PurchaseOrderId.Value);
        }
        return View(viewModel);
    }

    public async Task<IActionResult> PurchaseDetails(string id)
    {
        int decryptedId = DecryptId(id);
        var purchase = await _purchaseService.GetPurchaseById(decryptedId);
        if (purchase == null) return NotFound();
        return View(purchase);
    }
}
