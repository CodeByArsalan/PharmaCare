using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.Web.Controllers.SalesManagement;

[Authorize]
public class SaleController : BaseController
{
    private readonly ISaleService _saleService;
    private readonly IPartyService _partyService;
    private readonly IProductService _productService;
    private readonly IAccountService _accountService;

    public SaleController(
        ISaleService saleService,
        IPartyService partyService,
        IProductService productService,
        IAccountService accountService)
    {
        _saleService = saleService;
        _partyService = partyService;
        _productService = productService;
        _accountService = accountService;
    }

    public async Task<IActionResult> SalesIndex()
    {
        var sales = await _saleService.GetAllAsync();
        return View(sales);
    }

    public async Task<IActionResult> AddSale()
    {
        await LoadDropdownsAsync();
        return View(new StockMain
        {
            TransactionDate = DateTime.Now,
            StockDetails = new List<StockDetail> { new StockDetail() }
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSale(StockMain sale, int? PaymentAccountId)
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

        for (int i = 0; i < sale.StockDetails.Count; i++)
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
                // Load Party with Account for accounting entries (if customer selected)
                if (sale.Party_ID.HasValue && sale.Party_ID > 0)
                {
                    var party = await _partyService.GetByIdWithAccountAsync(sale.Party_ID.Value);
                    sale.Party = party;
                }

                await _saleService.CreateAsync(sale, CurrentUserId, PaymentAccountId);
                ShowMessage(MessageType.Success, "Sale created successfully!");
                return RedirectToAction(nameof(SalesIndex));
            }
            catch (Exception ex)
            {
                ShowMessage(MessageType.Error, "Error creating sale: " + ex.Message);
            }
        }

        await LoadDropdownsAsync();
        return View(sale);
    }

    public async Task<IActionResult> ViewSale(int id)
    {
        var sale = await _saleService.GetByIdAsync(id);
        if (sale == null)
        {
            ShowMessage(MessageType.Error, "Sale not found.");
            return RedirectToAction(nameof(SalesIndex));
        }

        return View(sale);
    }

    /// <summary>
    /// Voids a sale.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Void(int id, string voidReason)
    {
        if (string.IsNullOrWhiteSpace(voidReason))
        {
            ShowMessage(MessageType.Error, "Void reason is required.");
            return RedirectToAction(nameof(SalesIndex));
        }

        var result = await _saleService.VoidAsync(id, voidReason, CurrentUserId);
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

    /// <summary>
    /// Gets products as JSON for AJAX dropdown with calculated current stock.
    /// </summary>
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

        // Load Cash/Bank accounts for payment
        var accounts = await _accountService.GetCashBankAccountsAsync();
        ViewBag.PaymentAccounts = new SelectList(
            accounts,
            "AccountID",
            "Name"
        );

        // Load Price Types
        var priceTypes = await _productService.GetPriceTypesAsync();
        ViewBag.PriceTypes = new SelectList(
            priceTypes,
            "PriceTypeID",
            "PriceTypeName"
        );
    }
}
