using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Application.Interfaces.Finance;
using PharmaCare.Domain.Entities.Transactions;
using PharmaCare.Web.Utilities;

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
    private readonly IPaymentService _paymentService;

    public PurchaseOrderController(
        IPurchaseOrderService purchaseOrderService,
        IPartyService partyService,
        IProductService productService,
        IPaymentService paymentService)
    {
        _purchaseOrderService = purchaseOrderService;
        _partyService = partyService;
        _productService = productService;
        _paymentService = paymentService;
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
    public async Task<IActionResult> EditPurchaseOrder(string id)
    {
        int poId = Utility.DecryptId(id);
        if (poId == 0)
        {
             ShowMessage(MessageType.Error, "Invalid Purchase Order ID.");
             return RedirectToAction(nameof(PurchaseOrdersIndex));
        }

        var purchaseOrder = await _purchaseOrderService.GetByIdAsync(poId);
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
    public async Task<IActionResult> Approve(string id)
    {
        int poId = Utility.DecryptId(id);
        if (poId == 0)
        {
             ShowMessage(MessageType.Error, "Invalid Purchase Order ID.");
             return RedirectToAction(nameof(PurchaseOrdersIndex));
        }

        var result = await _purchaseOrderService.ApproveAsync(poId, CurrentUserId);
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
    public async Task<IActionResult> Delete(string id)
    {
        int poId = Utility.DecryptId(id);
        if (poId == 0)
        {
             ShowMessage(MessageType.Error, "Invalid Purchase Order ID.");
             return RedirectToAction(nameof(PurchaseOrdersIndex));
        }

        var result = await _purchaseOrderService.ToggleStatusAsync(poId, CurrentUserId);
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

    /// <summary>
    /// Shows form to make an advance payment against a Purchase Order.
    /// </summary>
    public async Task<IActionResult> MakePayment(string id)
    {
        int poId = Utility.DecryptId(id);
        if (poId == 0)
        {
             ShowMessage(MessageType.Error, "Invalid Purchase Order ID.");
             return RedirectToAction(nameof(PurchaseOrdersIndex));
        }

        var po = await _purchaseOrderService.GetByIdAsync(poId);
        if (po == null)
        {
            ShowMessage(MessageType.Error, "Purchase Order not found.");
            return RedirectToAction(nameof(PurchaseOrdersIndex));
        }

        if (po.Status != "Approved")
        {
            ShowMessage(MessageType.Warning, "Payments can only be made against Approved Purchase Orders.");
            return RedirectToAction(nameof(PurchaseOrdersIndex));
        }

        if (po.BalanceAmount <= 0)
        {
            ShowMessage(MessageType.Warning, "This Purchase Order is already fully paid.");
            return RedirectToAction(nameof(PurchaseOrdersIndex));
        }

        // Use IComboboxRepository in View for accounts
        ViewBag.PO = po;

        return View(new Domain.Entities.Finance.Payment
        {
            StockMain_ID = po.StockMainID,
            Party_ID = po.Party_ID ?? 0,
            Amount = po.BalanceAmount,
            PaymentDate = DateTime.Now,
            PaymentMethod = "Cash"
        });
    }

    /// <summary>
    /// Processes an advance payment against a Purchase Order.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MakePayment(Domain.Entities.Finance.Payment payment)
    {
        // Remove validation for navigation properties
        ModelState.Remove("Party");
        ModelState.Remove("StockMain");
        ModelState.Remove("Account");
        ModelState.Remove("Voucher");
        ModelState.Remove("PaymentType");
        ModelState.Remove("Reference");

        if (ModelState.IsValid)
        {
            try
            {
                // We use the existing PaymentService.CreatePaymentAsync
                // It handles "PAYMENT" type validation against StockMain balance.
                // Since PO is a StockMain, this works beautifully.
                await _paymentService.CreatePaymentAsync(payment, CurrentUserId);
                ShowMessage(MessageType.Success, "Advance Payment recorded successfully!");
                return RedirectToAction(nameof(PurchaseOrdersIndex));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
            }
        }

        // Reload PO for display if error
        var po = await _purchaseOrderService.GetByIdAsync(payment.StockMain_ID ?? 0);
        ViewBag.PO = po;
        return View(payment);
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
