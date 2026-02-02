using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.Web.Controllers.PurchaseManagement;

/// <summary>
/// Controller for managing Purchase Returns.
/// </summary>
[Authorize]
public class PurchaseReturnController : BaseController
{
    private readonly IPurchaseReturnService _purchaseReturnService;
    private readonly IPartyService _partyService;
    private readonly IProductService _productService;

    public PurchaseReturnController(
        IPurchaseReturnService purchaseReturnService,
        IPartyService partyService,
        IProductService productService)
    {
        _purchaseReturnService = purchaseReturnService;
        _partyService = partyService;
        _productService = productService;
    }

    /// <summary>
    /// Displays list of all purchase returns.
    /// </summary>
    public async Task<IActionResult> PurchaseReturnsIndex()
    {
        var returns = await _purchaseReturnService.GetAllAsync();
        return View(returns);
    }

    /// <summary>
    /// Shows form to create a new purchase return.
    /// </summary>
    public async Task<IActionResult> AddPurchaseReturn()
    {
        await LoadDropdownsAsync();
        return View(new StockMain
        {
            TransactionDate = DateTime.Now,
            StockDetails = new List<StockDetail> { new StockDetail() }
        });
    }

    /// <summary>
    /// Creates a new purchase return.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPurchaseReturn(StockMain purchaseReturn)
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

        for (int i = 0; i < purchaseReturn.StockDetails.Count; i++)
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
                await _purchaseReturnService.CreateAsync(purchaseReturn, CurrentUserId);
                ShowMessage(MessageType.Success, "Purchase Return created successfully!");
                return RedirectToAction(nameof(PurchaseReturnsIndex));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error creating purchase return: " + ex.Message);
            }
        }

        await LoadDropdownsAsync();
        return View(purchaseReturn);
    }

    /// <summary>
    /// Shows details of a purchase return.
    /// </summary>
    public async Task<IActionResult> ViewPurchaseReturn(int id)
    {
        var purchaseReturn = await _purchaseReturnService.GetByIdAsync(id);
        if (purchaseReturn == null)
        {
            ShowMessage(MessageType.Error, "Purchase Return not found.");
            return RedirectToAction(nameof(PurchaseReturnsIndex));
        }

        return View(purchaseReturn);
    }

    /// <summary>
    /// Voids a purchase return.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Void(int id, string voidReason)
    {
        if (string.IsNullOrWhiteSpace(voidReason))
        {
            ShowMessage(MessageType.Error, "Void reason is required.");
            return RedirectToAction(nameof(PurchaseReturnsIndex));
        }

        var result = await _purchaseReturnService.VoidAsync(id, voidReason, CurrentUserId);
        if (result)
        {
            ShowMessage(MessageType.Success, "Purchase Return voided successfully!");
        }
        else
        {
            ShowMessage(MessageType.Error, "Failed to void Purchase Return.");
        }

        return RedirectToAction(nameof(PurchaseReturnsIndex));
    }

    /// <summary>
    /// Gets GRNs available for return for a supplier.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetGrns(int? supplierId)
    {
        var grns = await _purchaseReturnService.GetGrnsForReturnAsync(supplierId);
        var result = grns.Select(g => new
        {
            id = g.StockMainID,
            transactionNo = g.TransactionNo,
            date = g.TransactionDate.ToString("dd/MM/yyyy"),
            total = g.TotalAmount,
            balance = g.BalanceAmount,
            details = g.StockDetails.Select(d => new
            {
                productId = d.Product_ID,
                productName = d.Product?.Name,
                quantity = d.Quantity,
                unitPrice = d.UnitPrice,
                costPrice = d.CostPrice,
                lineTotal = d.LineTotal
            })
        }).ToList();

        return Json(result);
    }

    /// <summary>
    /// Gets products as JSON for AJAX dropdown.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetProducts()
    {
        var products = await _productService.GetAllAsync();
        var result = products
            .Where(p => p.IsActive)
            .Select(p => new
            {
                id = p.ProductID,
                name = p.Name,
                costPrice = p.OpeningPrice
            })
            .ToList();

        return Json(result);
    }

    private async Task LoadDropdownsAsync()
    {
        // Load suppliers
        var parties = await _partyService.GetAllAsync();
        ViewBag.Suppliers = new SelectList(
            parties.Where(p => p.IsActive && p.PartyType == "Supplier"),
            "PartyID",
            "Name"
        );

        // Load products
        var products = await _productService.GetAllAsync();
        ViewBag.Products = new SelectList(
            products.Where(p => p.IsActive),
            "ProductID",
            "Name"
        );

        // Load available GRNs
        var grns = await _purchaseReturnService.GetGrnsForReturnAsync();
        ViewBag.Grns = new SelectList(
            grns,
            "StockMainID",
            "TransactionNo"
        );
    }
}
