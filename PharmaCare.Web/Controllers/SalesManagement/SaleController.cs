using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Domain.Entities.Transactions;
using PharmaCare.Web.Filters;
using PharmaCare.Web.Utilities;

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
                return RedirectToAction(nameof(Receipt), new { id = Utility.EncryptId(sale.StockMainID) });
            }
            catch (Exception ex)
            {
                ShowMessage(MessageType.Error, "Error creating sale: " + ex.Message);
            }
        }

        // await LoadDropdownsAsync(); // Removed
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

        return View(sale);
    }

}
