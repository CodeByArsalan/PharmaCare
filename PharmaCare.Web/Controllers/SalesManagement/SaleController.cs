using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using PharmaCare.Application.Exceptions;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Domain.Entities.Configuration;
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
    private readonly IProfitSettingsService _profitSettingsService;
    private readonly IPricingService _pricingService;
    private readonly ILogger<SaleController> _logger;

    public SaleController(
        ISaleService saleService,
        IPartyService partyService,
        IProductService productService,
        IProfitSettingsService profitSettingsService,
        IPricingService pricingService,
        ILogger<SaleController> logger)
    {
        _saleService = saleService;
        _partyService = partyService;
        _productService = productService;
        _profitSettingsService = profitSettingsService;
        _pricingService = pricingService;
        _logger = logger;
    }

    public async Task<IActionResult> SalesIndex(int? customerId, DateTime? fromDate, DateTime? toDate, string? status, int page = 1, int pageSize = 25)
    {
        page = NormalizePage(page);
        pageSize = NormalizePageSize(pageSize);
        var pagedResult = await _saleService.GetPagedAsync(customerId, fromDate, toDate, status, page, pageSize);

        ViewBag.Customers = await GetPartySelectListAsync(_partyService, "Customer", customerId);
        ViewBag.SelectedCustomer = customerId;
        ViewBag.FromDate = fromDate;
        ViewBag.ToDate = toDate;
        ViewBag.SelectedStatus = status;

        return View(pagedResult);
    }

    public IActionResult AddSale()
    {
        return View(new StockMain
        {
            TransactionDate = AppTime.Now
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSale(SaleCreateRequest request, int? PaymentAccountId, bool overrideCreditLimit = false)
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
        Party? selectedParty = null;
        if (sale.Party_ID.HasValue && sale.Party_ID.Value > 0)
        {
            selectedParty = await _partyService.GetByIdAsync(sale.Party_ID.Value);
        }

        if (selectedParty != null && !string.IsNullOrWhiteSpace(walkInName))
        {
            if (IsWalkingCustomerParty(selectedParty.Name)
                && !IsDefaultWalkInLabel(walkInName))
            {
                var walkInTag = $"Walk-in Name: {walkInName}";
                sale.Remarks = string.IsNullOrWhiteSpace(sale.Remarks)
                    ? walkInTag
                    : $"{sale.Remarks} | {walkInTag}";
            }
        }

        // Server-side wholesale: enforce box pricing (via the canonical resolver) and convert boxes → units for stock
        if (selectedParty?.IsWholeSale == true)
        {
            var settings = await _profitSettingsService.GetAsync();
            var wholesaleCosts = await _productService.GetLastGrnCostPricesAsync(
                sale.StockDetails.Select(d => d.Product_ID));
            var productsWithStock = await _productService.GetProductsWithStockAsync(priceTypeId: _pricingService.WholesalePriceTypeId);
            var productLookup = productsWithStock.ToDictionary(
                ps => ps.Product.ProductID,
                ps =>
                {
                    var cost = wholesaleCosts.TryGetValue(ps.Product.ProductID, out var wc) ? wc : ps.Product.OpeningPrice;
                    var resolved = _pricingService.Resolve(
                        _pricingService.WholesalePriceTypeId, cost, ps.Product.UnitsInPack, ps.SpecificPrice, settings);
                    return new { resolved, ps.Product.UnitsInPack };
                });

            foreach (var detail in sale.StockDetails)
            {
                if (productLookup.TryGetValue(detail.Product_ID, out var info))
                {
                    // User entered quantity in boxes — convert to units for stock deduction
                    var boxesOrdered = detail.Quantity;
                    detail.Quantity = boxesOrdered * info.UnitsInPack;
                    detail.UnitPrice = info.resolved.UnitPrice;
                    detail.LineTotal = boxesOrdered * info.resolved.BoxPrice;
                }
            }
        }

        // Margin floor (hard block): re-derive authoritative costs and reject any below-cost line.
        // Also normalises CostPrice/LineCost server-side so client-supplied values can't understate COGS.
        var authoritativeCosts = await _productService.GetLastGrnCostPricesAsync(
            sale.StockDetails.Select(d => d.Product_ID));
        foreach (var detail in sale.StockDetails)
        {
            var cost = authoritativeCosts.TryGetValue(detail.Product_ID, out var lc) ? lc : detail.CostPrice;
            detail.CostPrice = cost;
            detail.LineCost = detail.Quantity * cost;

            if (detail.UnitPrice < cost)
            {
                ModelState.AddModelError(nameof(request.StockDetails),
                    $"A line item's sale price ({detail.UnitPrice:N2}) is below its cost ({cost:N2}). Below-cost sales are not allowed.");
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

            // An override is only honoured for a user who may also void a sale — the same bar as
            // reversing one. A posted flag alone is never enough.
            var mayOverrideCreditLimit = HasPermission("Sale", "SalesIndex", "delete");

            await _saleService.CreateAsync(sale, CurrentUserId, PaymentAccountId,
                overrideCreditLimit && mayOverrideCreditLimit);

            // Stamped with the sale it belongs to — the receipt view refuses to show a tender
            // carried over from a different sale.
            TempData["ReceiptTenderedAmount"] = tenderedAmount.ToString();
            TempData["ReceiptTenderedSaleId"] = sale.StockMainID;

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
        catch (CreditLimitExceededException ex)
        {
            // Overridable, so it is reported separately from a hard failure: the client may offer
            // an authorised user the chance to confirm and re-submit. Users who cannot override
            // just see the message.
            _logger.LogWarning(ex, "Credit limit breach on sale for customer {CustomerId} by user {UserId}.",
                request.Party_ID, CurrentUserId);

            var canOverride = HasPermission("Sale", "SalesIndex", "delete");

            if (Request.Headers.XRequestedWith == "XMLHttpRequest")
            {
                return Json(new { success = false, creditLimitWarning = canOverride, message = ex.Message });
            }

            ModelState.AddModelError(string.Empty, ex.Message);
            ShowMessage(MessageType.Warning, ex.Message);
            SetSaleFormState(request, PaymentAccountId);
            return View(sale);
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
        if (!IsVoidReasonValid(voidReason, out var reasonError))
        {
            ShowMessage(MessageType.Error, reasonError);
            return RedirectToAction(nameof(SalesIndex));
        }

        try
        {
            var result = await _saleService.VoidAsync(saleId, voidReason.Trim(), CurrentUserId);
            ShowMessage(result ? MessageType.Success : MessageType.Error,
                result ? "Sale voided successfully!" : "Failed to void Sale.");
        }
        catch (Exception ex)
        {
            // An already-voided sale, an active return, or a closed period all throw here.
            ShowMessage(MessageType.Error, SafeErrorMessage(ex, "Voiding sale"));
        }

        return RedirectToAction(nameof(SalesIndex));
    }

    [HttpGet]
    [LinkedToPage("Sale", "SalesIndex")]
    public async Task<IActionResult> GetProducts(int? priceTypeId)
    {
        try
        {
            var productsWithStock = await _productService.GetProductsWithStockAsync(priceTypeId);
            var lastGrnCosts = await _productService.GetLastGrnCostPricesAsync(
                productsWithStock.Select(ps => ps.Product.ProductID));
            var profitSettings = await _profitSettingsService.GetAsync();

            // Default to retail (1) when no price type is specified.
            var priceType = priceTypeId ?? 1;
            var result = productsWithStock
                .Select(ps =>
                {
                    var costPrice = lastGrnCosts.TryGetValue(ps.Product.ProductID, out var c) ? c : ps.Product.OpeningPrice;

                    // Single canonical resolver: explicit price ?? cost+margin formula (rounded, never below cost).
                    var resolved = _pricingService.Resolve(priceType, costPrice, ps.Product.UnitsInPack, ps.SpecificPrice, profitSettings);

                    return new
                    {
                        id = ps.Product.ProductID,
                        name = ps.Product.Name,
                        unitPrice = resolved.UnitPrice,
                        costPrice = costPrice,
                        stockQuantity = ps.CurrentStock,
                        unitsInPack = ps.Product.UnitsInPack,
                        boxPrice = resolved.BoxPrice,
                        priceSource = resolved.Source.ToString(),
                        belowCost = resolved.BelowCost
                    };
                })
                .ToList();

            return Json(result);
        }
        catch (Exception ex)
        {
            // Never echo ex.Message/InnerException here — it can carry SQL text and server names,
            // and this endpoint is reachable by every authenticated user. SafeErrorMessage logs
            // the full exception and returns only what is safe to display.
            return StatusCode(500, new { message = SafeErrorMessage(ex, $"Loading products (priceTypeId {priceTypeId})") });
        }
    }

    [HttpGet]
    [LinkedToPage("Sale", "SalesIndex")]
    public async Task<IActionResult> GetCustomerInfo(int customerId)
    {
        if (customerId <= 0)
        {
            return Json(new
            {
                isWholeSale = false,
                outstandingBalance = 0m,
                openInvoices = 0,
                status = "Unknown"
            });
        }

        try
        {
            var party = await _partyService.GetByIdAsync(customerId);
            var summary = await _saleService.GetCustomerOutstandingSummaryAsync(customerId);

            return Json(new
            {
                isWholeSale = party?.IsWholeSale ?? false,
                outstandingBalance = summary.OutstandingBalance,
                openInvoices = summary.OpenInvoices,
                status = summary.OutstandingBalance > 0 ? "Outstanding" : "Clear"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching customer info for customer {CustomerId}", customerId);
            return Json(new
            {
                isWholeSale = false,
                outstandingBalance = 0m,
                openInvoices = 0,
                status = "Unavailable"
            });
        }
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

        // Read both every time so a stale pair is discarded rather than lingering for the next
        // receipt. The tendered amount belongs to exactly one sale: TempData.Keep below re-persists
        // it for reprints, which previously let the PREVIOUS sale's tender (and change) render on
        // an unrelated receipt. Without a match the view falls back to this sale's own PaidAmount.
        TempData.TryGetValue("ReceiptTenderedSaleId", out var tenderedSaleIdObj);
        TempData.TryGetValue("ReceiptTenderedAmount", out var tenderedAmountObj);

        if (int.TryParse(Convert.ToString(tenderedSaleIdObj), out var tenderedSaleId)
            && tenderedSaleId == saleId
            && decimal.TryParse(Convert.ToString(tenderedAmountObj), out var tenderedAmount))
        {
            ViewBag.TenderedAmount = tenderedAmount;
            TempData.Keep("ReceiptTenderedAmount");
            TempData.Keep("ReceiptTenderedSaleId");
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
