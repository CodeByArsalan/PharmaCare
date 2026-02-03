using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.Web.Controllers.SalesManagement;

/// <summary>
/// Controller for managing Sales.
/// </summary>
[Authorize]
public class SaleController : BaseController
{
    private readonly ISaleService _saleService;
    private readonly IPartyService _partyService;
    private readonly IProductService _productService;

    public SaleController(
        ISaleService saleService,
        IPartyService partyService,
        IProductService productService)
    {
        _saleService = saleService;
        _partyService = partyService;
        _productService = productService;
    }

    /// <summary>
    /// Displays list of all sales.
    /// </summary>
    public async Task<IActionResult> SalesIndex()
    {
        var sales = await _saleService.GetAllAsync();
        return View(sales);
    }

    /// <summary>
    /// Shows form to create a new sale.
    /// </summary>
    public async Task<IActionResult> AddSale()
    {
        await LoadDropdownsAsync();
        return View(new StockMain
        {
            TransactionDate = DateTime.Now,
            StockDetails = new List<StockDetail> { new StockDetail() }
        });
    }

    /// <summary>
    /// Creates a new sale.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSale(StockMain sale)
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

                await _saleService.CreateAsync(sale, CurrentUserId);
                ShowMessage(MessageType.Success, "Sale created successfully!");
                return RedirectToAction(nameof(SalesIndex));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error creating sale: " + ex.Message);
            }
        }

        await LoadDropdownsAsync();
        return View(sale);
    }

    /// <summary>
    /// Shows details of a sale.
    /// </summary>
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
                unitPrice = p.OpeningPrice
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
    }
}
