using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Domain.Entities.Transactions;
using PharmaCare.Web.Utilities;
using PharmaCare.Web.ViewModels.Transactions;

namespace PharmaCare.Web.Controllers.PurchaseManagement;

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

    public async Task<IActionResult> PurchaseReturnsIndex()
    {
        var returns = await _purchaseReturnService.GetAllAsync();
        return View(returns);
    }

    public  IActionResult AddPurchaseReturn()
    {
        //await LoadDropdownsAsync();
        return View(new StockMain
        {
            TransactionDate = DateTime.Now,
            StockDetails = new List<StockDetail> { new StockDetail() }
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPurchaseReturn(PurchaseReturnCreateRequest request)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var purchaseReturn = MapToStockMain(request);
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
        return View(MapToStockMain(request));
    }

    public async Task<IActionResult> ViewPurchaseReturn(string id)
    {
        int purchaseReturnId = Utility.DecryptId(id);
        if (purchaseReturnId == 0)
        {
            ShowMessage(MessageType.Error, "Invalid Purchase Return ID.");
            return RedirectToAction(nameof(PurchaseReturnsIndex));
        }

        var purchaseReturn = await _purchaseReturnService.GetByIdAsync(purchaseReturnId);
        if (purchaseReturn == null)
        {
            ShowMessage(MessageType.Error, "Purchase Return not found.");
            return RedirectToAction(nameof(PurchaseReturnsIndex));
        }

        return View(purchaseReturn);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Void(string id, string voidReason)
    {
        int purchaseReturnId = Utility.DecryptId(id);
        if (purchaseReturnId == 0)
        {
             ShowMessage(MessageType.Error, "Invalid Purchase Return ID.");
             return RedirectToAction(nameof(PurchaseReturnsIndex));
        }

        if (string.IsNullOrWhiteSpace(voidReason))
        {
            ShowMessage(MessageType.Error, "Void reason is required.");
            return RedirectToAction(nameof(PurchaseReturnsIndex));
        }

        var result = await _purchaseReturnService.VoidAsync(purchaseReturnId, voidReason, CurrentUserId);
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
                costPrice = d.CostPrice > 0 ? d.CostPrice : d.UnitPrice,
                lineTotal = d.LineTotal
            })
        }).ToList();

        return Json(result);
    }

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
        var products = await _productService.GetAllAsync();
        ViewBag.Products = new SelectList(
            products.Where(p => p.IsActive),
            "ProductID",
            "Name"
        );

        var grns = await _purchaseReturnService.GetGrnsForReturnAsync();
        ViewBag.Grns = new SelectList(
            grns,
            "StockMainID",
            "TransactionNo"
        );
    }

    /// <summary>
    /// Maps a PurchaseReturnCreateRequest DTO to a StockMain entity.
    /// </summary>
    private static StockMain MapToStockMain(PurchaseReturnCreateRequest request)
    {
        return new StockMain
        {
            TransactionDate = request.TransactionDate,
            Party_ID = request.Party_ID,
            ReferenceStockMain_ID = request.ReferenceStockMain_ID,
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
