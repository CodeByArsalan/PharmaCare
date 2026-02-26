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

namespace PharmaCare.Web.Controllers.SalesManagement;

[Authorize]
public class SaleController : BaseController
{
    private readonly ISaleService _saleService;
    private readonly IPartyService _partyService;
    private readonly IProductService _productService;
    private readonly IAccountService _accountService;
    private readonly ILogger<SaleController> _logger;

    public SaleController(
        ISaleService saleService,
        IPartyService partyService,
        IProductService productService,
        IAccountService accountService,
        ILogger<SaleController> logger)
    {
        _saleService = saleService;
        _partyService = partyService;
        _productService = productService;
        _accountService = accountService;
        _logger = logger;
    }

    public async Task<IActionResult> SalesIndex()
    {
        var sales = await _saleService.GetAllAsync();
        return View(sales);
    }

    public IActionResult AddSale()
    {
        // await LoadDropdownsAsync(); // Removed
        return View(new StockMain
        {
            TransactionDate = DateTime.Now,
            StockDetails = new List<StockDetail> { new StockDetail() }
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSale(SaleCreateRequest request, int? PaymentAccountId)
    {
        var tenderedAmount = Math.Round(request.TenderedAmount ?? request.PaidAmount, 2, MidpointRounding.AwayFromZero);
        if (tenderedAmount < 0)
        {
            ModelState.AddModelError(nameof(request.TenderedAmount), "Tendered amount cannot be negative.");
        }

        request.PaidAmount = Math.Max(0, tenderedAmount);

        if (request.StockDetails == null || request.StockDetails.Count == 0)
        {
            ModelState.AddModelError(nameof(request.StockDetails), "At least one item is required.");
        }
        if (!request.Party_ID.HasValue || request.Party_ID.Value <= 0)
        {
            ModelState.AddModelError(nameof(request.Party_ID), "Customer is required.");
        }

        var sale = MapToStockMain(request);
        sale.PaidAmount = Math.Max(0, tenderedAmount);

        var walkInName = request.WalkInCustomerName?.Trim();
        if (sale.Party_ID.HasValue && sale.Party_ID.Value > 0 && !string.IsNullOrWhiteSpace(walkInName))
        {
            var selectedParty = await _partyService.GetByIdAsync(sale.Party_ID.Value);
            if (selectedParty != null
                && IsWalkingCustomerParty(selectedParty.Name)
                && !IsDefaultWalkInLabel(walkInName))
            {
                var walkInTag = $"Walk-in Name: {walkInName}";
                sale.Remarks = string.IsNullOrWhiteSpace(sale.Remarks)
                    ? walkInTag
                    : $"{sale.Remarks} | {walkInTag}";
            }
        }

        if (!ModelState.IsValid)
        {
            if (Request.Headers.XRequestedWith == "XMLHttpRequest")
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return Json(new { success = false, message = string.Join(" ", errors) });
            }
            SetSaleFormState(request, PaymentAccountId);
            return View(sale);
        }

        try
        {
            // Load Party with Account for accounting entries (if customer selected)
            if (sale.Party_ID.HasValue && sale.Party_ID > 0)
            {
                var party = await _partyService.GetByIdWithAccountAsync(sale.Party_ID.Value);
                sale.Party = party;
            }

            await _saleService.CreateAsync(sale, CurrentUserId, PaymentAccountId);
            TempData["ReceiptTenderedAmount"] = tenderedAmount.ToString();

            var encryptedId = Utility.EncryptId(sale.StockMainID);
            var receiptUrl = Url.Action(nameof(Receipt), new { id = encryptedId })!;
            var salesIndexUrl = Url.Action(nameof(SalesIndex))!;

            // AJAX request: return JSON so JS can open receipt in new tab + redirect
            if (Request.Headers.XRequestedWith == "XMLHttpRequest")
            {
                return Json(new { success = true, receiptUrl, salesIndexUrl });
            }

            ShowMessage(MessageType.Success, "Sale created successfully!");
            return RedirectToAction(nameof(Receipt), new { id = encryptedId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create sale for user {UserId}.", CurrentUserId);

            var errorMessage = (ex is InvalidOperationException || ex is ArgumentException)
                ? ex.Message
                : "An unexpected error occurred while creating the sale.";

            if (Request.Headers.XRequestedWith == "XMLHttpRequest")
            {
                return Json(new { success = false, message = errorMessage });
            }

            ModelState.AddModelError(string.Empty, errorMessage);
            ShowMessage(MessageType.Error, errorMessage);
        }

        SetSaleFormState(request, PaymentAccountId);
        return View(sale);
    }

    public async Task<IActionResult> ViewSale(string id)
    {
        int saleId = Utility.DecryptId(id);
        if (saleId == 0)
        {
            ShowMessage(MessageType.Error, "Invalid Sale ID.");
            return RedirectToAction(nameof(SalesIndex));
        }

        var sale = await _saleService.GetByIdAsync(saleId);
        if (sale == null)
        {
            ShowMessage(MessageType.Error, "Sale not found.");
            return RedirectToAction(nameof(SalesIndex));
        }

        return View(sale);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [LinkedToPage("Sale", "SalesIndex", PermissionType = "delete")]
    public async Task<IActionResult> Void(string id, string voidReason)
    {
        int saleId = Utility.DecryptId(id);
        if (saleId == 0)
        {
             ShowMessage(MessageType.Error, "Invalid Sale ID.");
             return RedirectToAction(nameof(SalesIndex));
        }
        if (string.IsNullOrWhiteSpace(voidReason))
        {
            ShowMessage(MessageType.Error, "Void reason is required.");
            return RedirectToAction(nameof(SalesIndex));
        }

        var result = await _saleService.VoidAsync(saleId, voidReason, CurrentUserId);
        if (result)
        {
            ShowMessage(MessageType.Success, "Sale voided successfully!");
        }
        else
        {
            ShowMessage(MessageType.Error, "Failed to void Sale.");
        }

        return RedirectToAction(nameof(SalesIndex));
    }

    [HttpGet]
    public async Task<IActionResult> GetProducts(int? priceTypeId)
    {
        var productsWithStock = await _productService.GetProductsWithStockAsync(priceTypeId);
        var result = productsWithStock
            .Select(ps => new
            {
                id = ps.Product.ProductID,
                name = ps.Product.Name,
                unitPrice = ps.SpecificPrice ?? ps.Product.OpeningPrice,
                costPrice = ps.Product.OpeningPrice, // Use OpeningPrice as cost (can be enhanced later)
                stockQuantity = ps.CurrentStock
            })
            .ToList();

        return Json(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetCustomerCreditStatus(int customerId)
    {
        if (customerId <= 0)
        {
            return Json(new
            {
                outstandingBalance = 0m,
                openInvoices = 0,
                status = "Unknown"
            });
        }

        var summary = await _saleService.GetCustomerOutstandingSummaryAsync(customerId);
        var outstandingBalance = summary.OutstandingBalance;
        var openInvoices = summary.OpenInvoices;

        return Json(new
        {
            outstandingBalance,
            openInvoices,
            status = outstandingBalance > 0 ? "Outstanding" : "Clear"
        });
    }

    [LinkedToPage("Sale", "SalesIndex")]
    public async Task<IActionResult> Receipt(string id)
    {
        int saleId = Utility.DecryptId(id);
        if (saleId == 0)
        {
            ShowMessage(MessageType.Error, "Invalid Sale ID.");
            return RedirectToAction(nameof(SalesIndex));
        }

        var sale = await _saleService.GetByIdAsync(saleId);
        if (sale == null)
        {
            ShowMessage(MessageType.Error, "Sale not found.");
            return RedirectToAction(nameof(SalesIndex));
        }

        if (TempData.TryGetValue("ReceiptTenderedAmount", out var tenderedAmountObj)
            && decimal.TryParse(Convert.ToString(tenderedAmountObj), out var tenderedAmount))
        {
            ViewBag.TenderedAmount = tenderedAmount;
            TempData.Keep("ReceiptTenderedAmount");
        }

        return View(sale);
    }

    private static StockMain MapToStockMain(SaleCreateRequest request)
    {
        return new StockMain
        {
            TransactionDate = request.TransactionDate,
            Party_ID = request.Party_ID,
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

    private static bool IsWalkingCustomerParty(string? partyName)
    {
        if (string.IsNullOrWhiteSpace(partyName))
        {
            return false;
        }

        var normalized = partyName.Trim().ToLowerInvariant();
        return normalized.Contains("walkin")
               || normalized.Contains("walk-in")
               || normalized.Contains("walk in")
               || normalized.Contains("counter");
    }

    private static bool IsDefaultWalkInLabel(string? walkInName)
    {
        if (string.IsNullOrWhiteSpace(walkInName))
        {
            return true;
        }

        var normalized = walkInName.Trim().ToLowerInvariant();
        return normalized == "walk-in" || normalized == "walk in" || normalized == "walkin";
    }

    private void SetSaleFormState(SaleCreateRequest request, int? paymentAccountId)
    {
        ViewBag.TenderedAmount = request.TenderedAmount ?? request.PaidAmount;
        ViewBag.WalkInCustomerName = request.WalkInCustomerName;
        ViewBag.SelectedPaymentAccountId = paymentAccountId;
        ViewBag.SelectedPriceTypeId = request.PriceTypeId;
    }
}
