using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Domain.Entities.Transactions;
using PharmaCare.Web.Utilities;

namespace PharmaCare.Web.Controllers.SalesManagement;

/// <summary>
/// Controller for managing Sale Returns.
/// </summary>
[Authorize]
public class SaleReturnController : BaseController
{
    private readonly ISaleReturnService _saleReturnService;
    private readonly IPartyService _partyService;
    private readonly IProductService _productService;

    public SaleReturnController(
        ISaleReturnService saleReturnService,
        IPartyService partyService,
        IProductService productService)
    {
        _saleReturnService = saleReturnService;
        _partyService = partyService;
        _productService = productService;
    }

    /// <summary>
    /// Displays list of all sale returns.
    /// </summary>
    public async Task<IActionResult> SaleReturnsIndex()
    {
        var returns = await _saleReturnService.GetAllAsync();
        return View(returns);
    }

    /// <summary>
    /// Shows form to create a new sale return.
    /// </summary>
    public async Task<IActionResult> AddSaleReturn()
    {
        await LoadDropdownsAsync();
        return View(new StockMain
        {
            TransactionDate = DateTime.Now,
            StockDetails = new List<StockDetail> { new StockDetail() }
        });
    }

    /// <summary>
    /// Creates a new sale return.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSaleReturn(StockMain saleReturn)
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

        for (int i = 0; i < saleReturn.StockDetails.Count; i++)
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
                await _saleReturnService.CreateAsync(saleReturn, CurrentUserId);
                ShowMessage(MessageType.Success, "Sale Return created successfully!");
                return RedirectToAction(nameof(SaleReturnsIndex));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error creating sale return: " + ex.Message);
            }
        }

        await LoadDropdownsAsync();
        return View(saleReturn);
    }

    /// <summary>
    /// Shows details of a sale return.
    /// </summary>
    public async Task<IActionResult> ViewSaleReturn(string id)
    {
        int saleReturnId = Utility.DecryptId(id);
        if (saleReturnId == 0)
        {
            ShowMessage(MessageType.Error, "Invalid Sale Return ID.");
            return RedirectToAction(nameof(SaleReturnsIndex));
        }

        var saleReturn = await _saleReturnService.GetByIdAsync(saleReturnId);
        if (saleReturn == null)
        {
            ShowMessage(MessageType.Error, "Sale Return not found.");
            return RedirectToAction(nameof(SaleReturnsIndex));
        }

        return View(saleReturn);
    }

    /// <summary>
    /// Voids a sale return.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Void(string id, string voidReason)
    {
        int saleReturnId = Utility.DecryptId(id);
        if (saleReturnId == 0)
        {
             ShowMessage(MessageType.Error, "Invalid Sale Return ID.");
             return RedirectToAction(nameof(SaleReturnsIndex));
        }
        if (string.IsNullOrWhiteSpace(voidReason))
        {
            ShowMessage(MessageType.Error, "Void reason is required.");
            return RedirectToAction(nameof(SaleReturnsIndex));
        }

        var result = await _saleReturnService.VoidAsync(saleReturnId, voidReason, CurrentUserId);
        if (result)
        {
            ShowMessage(MessageType.Success, "Sale Return voided successfully!");
        }
        else
        {
            ShowMessage(MessageType.Error, "Failed to void Sale Return.");
        }

        return RedirectToAction(nameof(SaleReturnsIndex));
    }

    /// <summary>
    /// Gets sales available for return for a customer.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetSales(int? customerId)
    {
        var sales = await _saleReturnService.GetSalesForReturnAsync(customerId);
        var result = sales.Select(s => new
        {
            id = s.StockMainID,
            transactionNo = s.TransactionNo,
            date = s.TransactionDate.ToString("dd/MM/yyyy"),
            total = s.TotalAmount,
            balance = s.BalanceAmount,
            details = s.StockDetails.Select(d => new
            {
                productId = d.Product_ID,
                productName = d.Product?.Name,
                quantity = d.Quantity,
                unitPrice = d.UnitPrice,
                costPrice = d.CostPrice,
                lineTotal = d.LineTotal,
                lineCost = d.LineCost
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
                unitPrice = p.OpeningPrice,
                costPrice = p.OpeningPrice
            })
            .ToList();

        return Json(result);
    }

    private async Task LoadDropdownsAsync()
    {
        // Load customers
        var parties = await _partyService.GetAllAsync();
        ViewBag.Customers = new SelectList(
            parties.Where(p => p.IsActive && p.PartyType == "Customer"),
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

        // Load available sales for return
        var sales = await _saleReturnService.GetSalesForReturnAsync();
        ViewBag.Sales = new SelectList(
            sales,
            "StockMainID",
            "TransactionNo"
        );
    }
}
