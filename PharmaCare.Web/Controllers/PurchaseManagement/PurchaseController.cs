using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Domain.Entities.Transactions;
using PharmaCare.Web.Filters;
using PharmaCare.Web.Utilities;
using PharmaCare.Web.ViewModels.Transactions;

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
    private readonly ILogger<PurchaseController> _logger;

    public PurchaseController(
        IPurchaseService purchaseService,
        IPartyService partyService,
        IProductService productService,
        IAccountService accountService,
        ILogger<PurchaseController> logger)
    {
        _purchaseService = purchaseService;
        _partyService = partyService;
        _productService = productService;
        _accountService = accountService;
        _logger = logger;
    }

    public async Task<IActionResult> PurchasesIndex()
    {
        var purchases = await _purchaseService.GetAllAsync();
        return View(purchases);
    }

    public IActionResult AddPurchase()
    {
        // await LoadDropdownsAsync(); // REMOVED
        return View(new StockMain
        {
            TransactionDate = DateTime.Now,
            StockDetails = new List<StockDetail> { new StockDetail() }
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPurchase(PurchaseCreateRequest request, int? PaymentAccountId, decimal transferredAdvanceAmount = 0)
    {
        if (request.StockDetails == null || request.StockDetails.Count == 0)
        {
            ModelState.AddModelError(nameof(request.StockDetails), "At least one item is required.");
        }

        var purchase = MapToStockMain(request);
        if (!ModelState.IsValid)
        {
            return View(purchase);
        }

        try
        {
            await _purchaseService.CreateAsync(purchase, CurrentUserId, PaymentAccountId, transferredAdvanceAmount);
            ShowMessage(MessageType.Success, "Purchase created successfully!");
            return RedirectToAction(nameof(PurchasesIndex));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create purchase for user {UserId}.", CurrentUserId);
            ModelState.AddModelError("", "An unexpected error occurred while creating the purchase.");
        }

        return View(purchase);
    }

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

    [HttpPost]
    [ValidateAntiForgeryToken]
    [LinkedToPage("Purchase", "PurchasesIndex", PermissionType = "delete")]
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

    [HttpGet]
    [LinkedToPage("Purchase", "PurchasesIndex")]
    public async Task<IActionResult> GetPurchaseOrders(int supplierId)
    {
        var pos = await _purchaseService.GetPurchaseOrdersForGrnAsync(supplierId);

        var result = pos.Select(po => new
        {
            id = po.StockMainID,
            transactionNo = po.TransactionNo,
            date = po.TransactionDate.ToString("dd/MM/yyyy"),
            total = po.TotalAmount,
            paidAmount = po.PaidAmount,
            details = po.StockDetails.Select(d => new
            {
                productId = d.Product_ID,
                quantity = d.Quantity,
                costPrice = d.CostPrice,
                lineTotal = d.LineTotal
            })
        });

        return Json(result);
    }

    [HttpGet]
    [LinkedToPage("Purchase", "PurchasesIndex")]
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

    private static StockMain MapToStockMain(PurchaseCreateRequest request)
    {
        return new StockMain
        {
            TransactionDate = request.TransactionDate,
            Party_ID = request.Party_ID,
            ReferenceStockMain_ID = request.ReferenceStockMain_ID,
            DiscountPercent = request.DiscountPercent,
            PaidAmount = request.PaidAmount,
            Remarks = request.Remarks,
            StockDetails = request.StockDetails.Select(d => new StockDetail
            {
                Product_ID = d.Product_ID,
                Quantity = d.Quantity,
                UnitPrice = d.UnitPrice,
                CostPrice = d.CostPrice,
                DiscountPercent = d.DiscountPercent,
                DiscountAmount = d.DiscountAmount,
                LineTotal = d.LineTotal,
                LineCost = d.LineCost,
                Remarks = d.Remarks
            }).ToList()
        };
    }
}
