using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Domain.Entities.Transactions;

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
    public async Task<IActionResult> ViewPurchase(int id)
    {
        var purchase = await _purchaseService.GetByIdAsync(id);
        if (purchase == null)
        {
            ShowMessage(MessageType.Error, "Purchase not found.");
            return RedirectToAction(nameof(PurchasesIndex));
        }

        return View(purchase);
    }

    // ... [Void and other methods unchanged] ...

    // Dropdown helpers removed
}
