using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Domain.Entities.Transactions;
using PharmaCare.Web.Utilities;

namespace PharmaCare.Web.Controllers.PurchaseManagement;

/// <summary>
/// Controller for managing Purchases (GRN - Goods Received Notes).
/// </summary>
[Authorize]
public class PurchaseController : BaseController
{
    private readonly IPurchaseService _purchaseService;
    private readonly IPartyService _partyService;
    private readonly IProductService _productService;
    private readonly IAccountService _accountService;

    public PurchaseController(
        IPurchaseService purchaseService,
        IPartyService partyService,
        IProductService productService,
        IAccountService accountService)
    {
        _purchaseService = purchaseService;
        _partyService = partyService;
        _productService = productService;
        _accountService = accountService;
    }

    /// <summary>
    /// Displays list of all purchases/GRNs.
    /// </summary>
    public async Task<IActionResult> PurchasesIndex()
    {
        var purchases = await _purchaseService.GetAllAsync();
        return View(purchases);
    }

    /// <summary>
    /// Shows form to create a new purchase/GRN.
    /// </summary>
    public IActionResult AddPurchase()
    {
        // await LoadDropdownsAsync(); // REMOVED
        return View(new StockMain
        {
            TransactionDate = DateTime.Now,
            StockDetails = new List<StockDetail> { new StockDetail() }
        });
    }

    /// <summary>
    /// Creates a new purchase/GRN.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPurchase(StockMain purchase, int? PaymentAccountId)
    {
        // Remove validation for navigation properties
        ModelState.Remove("TransactionType");
        ModelState.Remove("Party");
        ModelState.Remove("Voucher");
        ModelState.Remove("ReferenceStockMain");
        
        // Remove validation for auto-generated/computed fields
        ModelState.Remove("TransactionNo");
        ModelState.Remove("Status");
        ModelState.Remove("PaymentStatus");
        ModelState.Remove("StockMainID");

        for (int i = 0; i < purchase.StockDetails.Count; i++)
        {
            ModelState.Remove($"StockDetails[{i}].StockMain");
            ModelState.Remove($"StockDetails[{i}].Product");
            ModelState.Remove($"StockDetails[{i}].StockDetailID");
            ModelState.Remove($"StockDetails[{i}].StockMain_ID");
        }

        if (ModelState.IsValid)
        {
            try
            {
                await _purchaseService.CreateAsync(purchase, CurrentUserId, PaymentAccountId);
                ShowMessage(MessageType.Success, "Purchase created successfully!");
                return RedirectToAction(nameof(PurchasesIndex));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error creating purchase: " + ex.Message);
            }
        }

        // await LoadDropdownsAsync(); // REMOVED
        return View(purchase);
    }

    /// <summary>
    /// Shows details of a purchase/GRN.
    /// </summary>
    public async Task<IActionResult> ViewPurchase(string id)
    {
        int purchaseId = Utility.DecryptId(id);
        if (purchaseId == 0)
        {
            ShowMessage(MessageType.Error, "Invalid Purchase ID.");
            return RedirectToAction(nameof(PurchasesIndex));
        }

        var purchase = await _purchaseService.GetByIdAsync(purchaseId);
        if (purchase == null)
        {
            ShowMessage(MessageType.Error, "Purchase not found.");
            return RedirectToAction(nameof(PurchasesIndex));
        }

        return View(purchase);
    }

    /// <summary>
    /// Voids a purchase/GRN.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Void(string id, string voidReason)
    {
        int purchaseId = Utility.DecryptId(id);
        if (purchaseId == 0)
        {
            ShowMessage(MessageType.Error, "Invalid Purchase ID.");
            return RedirectToAction(nameof(PurchasesIndex));
        }

        if (string.IsNullOrWhiteSpace(voidReason))
        {
            ShowMessage(MessageType.Error, "Void reason is required.");
            return RedirectToAction(nameof(PurchasesIndex));
        }

        var result = await _purchaseService.VoidAsync(purchaseId, voidReason, CurrentUserId);
        if (result)
        {
            ShowMessage(MessageType.Success, "Purchase voided successfully!");
        }
        else
        {
            ShowMessage(MessageType.Error, "Failed to void Purchase.");
        }

        return RedirectToAction(nameof(PurchasesIndex));
    }

    /// <summary>
    /// Gets approved Purchase Orders for a supplier (AJAX).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetPurchaseOrders(int supplierId)
    {
        // Use generic PurchaseOrderService or directly repository?
        // PurchaseOrderService.GetApprovedPurchaseOrdersAsync(supplierId) exists.
        // But we need to inject IPurchaseOrderService.
        // OR we can use _purchaseService if we add a method there.
        // Let's check if IPurchaseOrderService is available here.
        // It is NOT injected.
        // We should add it to constructor.
        
        // For now, let's assume we will inject it.
        // Wait, modifying constructor is risky if I don't see the file.
        // I have _purchaseService. Let's add GetPurchaseOrdersForGrnAsync to IPurchaseService/PurchaseService?
        // It ALREADY exists: GetPurchaseOrdersForGrnAsync(int? supplierId).
        
        var pos = await _purchaseService.GetPurchaseOrdersForGrnAsync(supplierId);
        
        // Now we need to calculate PaidAmount (Advance) for each PO.
        // We can do this by querying payments.
        // This might be N+1, but for dropdown it's okay.
        
        // Better: Fetch all advance payments for this supplier and map them.
        // Or simply iterate.
        // We don't have _paymentRepository exposed here.
        // But PurchaseService returns StockMain, which has StockDetails.
        // It does NOT have Payments included by default in that method?
        // Let's check PurchaseService.GetPurchaseOrdersForGrnAsync at step 550.
        // It includes TransactionType, Party, StockDetails.Trans.
        // It does NOT include Payments.
        
        // We can't easily get payments here without IPaymentService.
        // AND we can't easily inject it without seeing the full file again to check naming.
        // Actually I have the full file content from step 554.
        
        // Let's just return the POs for now, and rely on `PaidAmount` property of StockMain.
        // Does StockMain.PaidAmount reflect the advance payments?
        // In PurchaseOrderController.MakePayment, we create a Payment.
        // But we do NOT update StockMain.PaidAmount in `MakePayment`?
        // Let's check `PaymentService.CreatePaymentAsync` at step 497.
        // Line 145: stockMain.PaidAmount += payment.Amount;
        // YES! It updates the StockMain.
        // So `po.PaidAmount` ALREADY contains the advance payments.
        
        var result = pos.Select(po => new
        {
            id = po.StockMainID,
            transactionNo = po.TransactionNo,
            date = po.TransactionDate.ToString("dd/MM/yyyy"),
            total = po.TotalAmount,
            paidAmount = po.PaidAmount, // This is the advance!
            details = po.StockDetails.Select(d => new
            {
                productId = d.Product_ID,
                quantity = d.Quantity - 0, // TODO: Subtract already received qty if partial GRN allowed? System seems 1-to-1 PO-GRN for now.
                costPrice = d.CostPrice,
                lineTotal = d.LineTotal
            })
        });

        return Json(result);
    }
}
