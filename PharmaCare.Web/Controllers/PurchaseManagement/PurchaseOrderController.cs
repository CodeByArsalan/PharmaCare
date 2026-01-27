using Microsoft.AspNetCore.Mvc;
using PharmaCare.Application.Interfaces.Inventory;
using PharmaCare.Domain.Models.Inventory;
using PharmaCare.Domain.Models.PurchaseManagement;
using PharmaCare.Web.Utilities;
using PharmaCare.Infrastructure.Interfaces;

using PharmaCare.Application.Interfaces.PurchaseManagement;

namespace PharmaCare.Web.Controllers.PurchaseManagement;

public class PurchaseOrderController : BaseController
{
    private readonly IPurchaseOrderService _poService;
    private readonly IComboBoxRepository _comboBox;

    public PurchaseOrderController(IPurchaseOrderService poService, IComboBoxRepository comboBox)
    {
        _poService = poService;
        _comboBox = comboBox;
    }
    public async Task<IActionResult> PurchaseOrderIndex()
    {
        var orders = await _poService.GetPurchaseOrders();
        return View(orders);
    }
    [HttpGet]
    public async Task<IActionResult> AddPurchaseOrder()
    {
        var po = new PurchaseOrder();
        po.PurchaseOrderNumber = await _poService.GeneratePurchaseOrderNumber();
        return View(po);
    }
    [HttpPost]
    public async Task<IActionResult> AddPurchaseOrder(PurchaseOrder purchaseOrder)
    {
        // Manual validation might be needed for Items if not bound correctly

        purchaseOrder.Party = null; // Prevent re-validation issues or tracking conflicts
        if (await _poService.CreatePurchaseOrder(purchaseOrder, LoginUserID))
        {
            ShowMessage(MessageBox.Success, "Purchase Order created successfully.");
            return RedirectToAction(nameof(PurchaseOrderIndex));
        }
        ViewBag.Suppliers = await _comboBox.GetSuppliersAsync();
        return View(purchaseOrder);
    }
    [HttpGet]
    public async Task<IActionResult> EditPurchaseOrder(string id)
    {
        int decryptedId = DecryptId(id);
        var po = await _poService.GetPurchaseOrderById(decryptedId);
        if (po == null) return NotFound();
        if (po.Status != "Pending")
        {
            ShowMessage(MessageBox.Error, "Only Pending orders can be edited.");
            return RedirectToAction(nameof(PurchaseOrderIndex));
        }
        return View(po);
    }
    [HttpPost]
    public async Task<IActionResult> EditPurchaseOrder(PurchaseOrder purchaseOrder)
    {
        purchaseOrder.Party = null;
        if (await _poService.UpdatePurchaseOrder(purchaseOrder, LoginUserID))
        {
            ShowMessage(MessageBox.Success, "Purchase Order updated successfully.");
            return RedirectToAction(nameof(PurchaseOrderIndex));
        }
        ViewBag.Suppliers = await _comboBox.GetSuppliersAsync();
        return View(purchaseOrder);
    }
    public async Task<IActionResult> PurchaseOrderDetails(string id)
    {
        int decryptedId = DecryptId(id);
        var po = await _poService.GetPurchaseOrderById(decryptedId);
        if (po == null) return NotFound();
        return View(po);
    }
}

