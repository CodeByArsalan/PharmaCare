using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.Web.Controllers.PurchaseManagement;

/// <summary>
/// Controller for managing Purchase Orders.
/// </summary>
[Authorize]
public class PurchaseOrderController : BaseController
{
    private readonly IPurchaseOrderService _purchaseOrderService;
    private readonly IPartyService _partyService;
    private readonly IProductService _productService;

    public PurchaseOrderController(
        IPurchaseOrderService purchaseOrderService,
        IPartyService partyService,
        IProductService productService)
    {
        _purchaseOrderService = purchaseOrderService;
        _partyService = partyService;
        _productService = productService;
    }

    /// <summary>
    /// Displays list of all purchase orders.
    /// </summary>
    public async Task<IActionResult> PurchaseOrdersIndex()
    {
        var purchaseOrders = await _purchaseOrderService.GetAllAsync();
        return View(purchaseOrders);
    }

    /// <summary>
    /// Shows form to create a new purchase order.
    /// </summary>
    public async Task<IActionResult> AddPurchaseOrder()
    {
        await LoadDropdownsAsync();
        return View(new StockMain
        {
            TransactionDate = DateTime.Now,
            StockDetails = new List<StockDetail> { new StockDetail() }
        });
    }

    /// <summary>
    /// Creates a new purchase order.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPurchaseOrder(StockMain purchaseOrder)
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

        // Remove validation for detail navigation properties
        for (int i = 0; i < purchaseOrder.StockDetails.Count; i++)
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
                await _purchaseOrderService.CreateAsync(purchaseOrder, CurrentUserId);
                ShowMessage(MessageType.Success, "Purchase Order created successfully!");
                return RedirectToAction(nameof(PurchaseOrdersIndex));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error creating purchase order: " + ex.Message);
            }
        }

        await LoadDropdownsAsync();
        return View(purchaseOrder);
    }

    /// <summary>
    /// Shows form to edit an existing purchase order.
    /// </summary>
    public async Task<IActionResult> EditPurchaseOrder(int id)
    {
        var purchaseOrder = await _purchaseOrderService.GetByIdAsync(id);
        if (purchaseOrder == null)
        {
            ShowMessage(MessageType.Error, "Purchase Order not found.");
            return RedirectToAction(nameof(PurchaseOrdersIndex));
        }

        if (purchaseOrder.Status != "Draft")
        {
            ShowMessage(MessageType.Warning, "Only draft purchase orders can be edited.");
            return RedirectToAction(nameof(PurchaseOrdersIndex));
        }

        await LoadDropdownsAsync();
        return View(purchaseOrder);
    }

    /// <summary>
    /// Updates an existing purchase order.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditPurchaseOrder(StockMain purchaseOrder)
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

        for (int i = 0; i < purchaseOrder.StockDetails.Count; i++)
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
                await _purchaseOrderService.UpdateAsync(purchaseOrder, CurrentUserId);
                ShowMessage(MessageType.Success, "Purchase Order updated successfully!");
                return RedirectToAction(nameof(PurchaseOrdersIndex));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error updating purchase order: " + ex.Message);
            }
        }

        await LoadDropdownsAsync();
        return View(purchaseOrder);
    }

    /// <summary>
    /// Approves a purchase order.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id)
    {
        var result = await _purchaseOrderService.ApproveAsync(id, CurrentUserId);
        if (result)
        {
            ShowMessage(MessageType.Success, "Purchase Order approved successfully!");
        }
        else
        {
            ShowMessage(MessageType.Error, "Failed to approve Purchase Order.");
        }

        return RedirectToAction(nameof(PurchaseOrdersIndex));
    }

    /// <summary>
    /// Deletes (voids) a purchase order.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _purchaseOrderService.ToggleStatusAsync(id, CurrentUserId);
        if (result)
        {
            ShowMessage(MessageType.Success, "Purchase Order cancelled successfully!");
        }
        else
        {
            ShowMessage(MessageType.Error, "Failed to cancel Purchase Order.");
        }

        return RedirectToAction(nameof(PurchaseOrdersIndex));
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
        // Load suppliers (parties with PartyType = "Supplier")
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
    }
}
