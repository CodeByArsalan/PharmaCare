using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Application.Interfaces.Inventory;
using PharmaCare.Domain.Models.Inventory;
using PharmaCare.Domain.Models.PurchaseManagement;
using PharmaCare.Web.Utilities;

namespace PharmaCare.Web.Controllers.PurchaseManagement;

public class PurchaseReturnController : BaseController
{
    private readonly IStockService _stockService;
    private readonly IStoreService _storeService;

    public PurchaseReturnController(IStockService stockService, IStoreService storeService)
    {
        _stockService = stockService;
        _storeService = storeService;
    }
    public async Task<IActionResult> PurchaseReturnIndex()
    {
        var returns = await _stockService.GetPurchaseReturns();
        return View(returns);
    }
    [HttpGet]
    public IActionResult AddPurchaseReturn()
    {
        return View(new PurchaseReturn());
    }
    [HttpPost]
    public async Task<IActionResult> AddPurchaseReturn(PurchaseReturn model)
    {
        if (ModelState.IsValid)
        {
            if (await _stockService.CreatePurchaseReturn(model, LoginUserID))
            {
                ShowMessage(MessageBox.Success, "Purchase Return created successfully.");
                return RedirectToAction(nameof(PurchaseReturnIndex));
            }
            ShowMessage(MessageBox.Error, "Failed to create Purchase Return.");
        }
        return View(model);
    }
    [HttpGet]
    public async Task<IActionResult> GetBatchesForSupplier(int supplierId, int storeId)
    {
        var batches = await _stockService.GetReturnableItems(supplierId, storeId);
        return Json(batches);
    }
    [HttpPost]
    public async Task<IActionResult> Approve(string id)
    {
        int decryptedId = DecryptId(id);
        if (await _stockService.ApprovePurchaseReturn(decryptedId, LoginUserID))
        {
            ShowMessage(MessageBox.Success, "Purchase Return approved and stock deducted.");
        }
        else
        {
            ShowMessage(MessageBox.Error, "Failed to approve. Check stock levels.");
        }
        return RedirectToAction(nameof(PurchaseReturnIndex));
    }
    public async Task<IActionResult> PurchaseReturnDetails(string id)
    {
        int decryptedId = DecryptId(id);
        ViewBag.LoginUserName = LoginUserName;
        var returnModel = await _stockService.GetPurchaseReturn(decryptedId);
        if (returnModel == null) return NotFound();
        return View(returnModel);
    }

    [HttpPost]
    public async Task<IActionResult> ProcessRefund(string id, string refundMethod)
    {
        int decryptedId = DecryptId(id);

        if (string.IsNullOrEmpty(refundMethod) || (refundMethod != "Cash" && refundMethod != "Bank"))
        {
            ShowMessage(MessageBox.Error, "Please select a valid refund method (Cash or Bank).");
            return RedirectToAction(nameof(PurchaseReturnDetails), new { id });
        }

        if (await _stockService.ProcessSupplierRefund(decryptedId, refundMethod, LoginUserID))
        {
            ShowMessage(MessageBox.Success, $"Refund processed successfully via {refundMethod}. Journal entry created.");
        }
        else
        {
            ShowMessage(MessageBox.Error, "Failed to process refund. Check if the return is approved and refund is pending.");
        }

        return RedirectToAction(nameof(PurchaseReturnDetails), new { id });
    }
}
