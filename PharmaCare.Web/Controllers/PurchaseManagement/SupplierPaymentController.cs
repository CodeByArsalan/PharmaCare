using Microsoft.AspNetCore.Mvc;
using PharmaCare.Application.Interfaces.Finance;
using PharmaCare.Domain.Models.Finance;
using PharmaCare.Web.Utilities;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaCare.Application.Interfaces.Configuration;

namespace PharmaCare.Web.Controllers.PurchaseManagement;

public class SupplierPaymentController : BaseController
{
    private readonly ISupplierPaymentService _paymentService;
    private readonly IPartyService _partyService;

    public SupplierPaymentController(
        ISupplierPaymentService paymentService,
        IPartyService partyService)
    {
        _paymentService = paymentService;
        _partyService = partyService;
    }

    public async Task<IActionResult> SupplierPaymentIndex(int? supplierId = null, string? status = null)
    {
        ViewBag.Summary = await _paymentService.GetPaymentSummary(LoginUserStoreID);
        ViewBag.Suppliers = new SelectList(await _partyService.GetSuppliers(), "PartyID", "PartyName", supplierId);
        ViewBag.SelectedSupplierId = supplierId;
        ViewBag.SelectedStatus = status;

        var payments = await _paymentService.GetAllPayments(supplierId, status);
        return View(payments);
    }
    public async Task<IActionResult> OutstandingGrns(int? supplierId = null)
    {
        ViewBag.Suppliers = new SelectList(await _partyService.GetSuppliers(), "PartyID", "PartyName", supplierId);
        ViewBag.SelectedSupplierId = supplierId;

        var outstandingGrns = await _paymentService.GetOutstandingGrns(supplierId);
        return View(outstandingGrns);
    }
    [HttpGet]
    public async Task<IActionResult> AddSupplierPayment(string? grnId)
    {
        var payment = new SupplierPayment
        {
            PaymentDate = DateTime.Now,
            Store_ID = LoginUserStoreID
        };

        if (!string.IsNullOrEmpty(grnId))
        {
            int decryptedGrnId = DecryptId(grnId);
            var outstanding = await _paymentService.GetOutstandingGrns();
            var grn = outstanding.FirstOrDefault(g => g.StockMainID == decryptedGrnId);

            if (grn != null)
            {
                payment.StockMain_ID = grn.StockMainID;
                payment.Party_ID = grn.SupplierId;
                payment.GrnAmount = grn.TotalAmount;
                payment.AmountPaid = grn.BalanceAmount; // Default to full payment

                ViewBag.GrnInfo = grn;
            }
        }

        await PopulateDropdowns(payment.Party_ID);
        return View(payment);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSupplierPayment(SupplierPayment payment)
    {
        try
        {
            payment.Store_ID = LoginUserStoreID;

            if (await _paymentService.CreatePayment(payment, LoginUserID))
            {
                ShowMessage(MessageBox.Success, "Payment recorded successfully.");
                return RedirectToAction(nameof(SupplierPaymentIndex));
            }

            ShowMessage(MessageBox.Error, "Failed to create payment.");
        }
        catch (InvalidOperationException ex)
        {
            ShowMessage(MessageBox.Error, ex.Message);
        }
        catch (Exception ex)
        {
            ShowMessage(MessageBox.Error, $"An error occurred: {ex.Message}");
        }

        await PopulateDropdowns(payment.Party_ID);
        return View(payment);
    }
    public async Task<IActionResult> SupplierPaymentDetails(string id)
    {
        int decryptedId = DecryptId(id);
        var payment = await _paymentService.GetPaymentById(decryptedId);

        if (payment == null)
            return NotFound();

        return View(payment);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelSupplierPayment(int id)
    {
        try
        {
            if (await _paymentService.CancelPayment(id, LoginUserID))
            {
                ShowMessage(MessageBox.Success, "Payment cancelled successfully.");
            }
            else
            {
                ShowMessage(MessageBox.Error, "Failed to cancel payment.");
            }
        }
        catch (InvalidOperationException ex)
        {
            ShowMessage(MessageBox.Error, ex.Message);
        }

        return RedirectToAction(nameof(SupplierPaymentIndex));
    }
    [HttpGet]
    public async Task<IActionResult> GetGrnDetails(int grnId)
    {
        var outstanding = await _paymentService.GetOutstandingGrns();
        var grn = outstanding.FirstOrDefault(g => g.StockMainID == grnId);

        if (grn == null)
            return Json(new { success = false, message = "GRN not found" });

        return Json(new
        {
            success = true,
            data = new
            {
                stockMainId = grn.StockMainID,
                grnNumber = grn.GrnNumber,
                supplierName = grn.SupplierName,
                totalAmount = grn.TotalAmount,
                amountPaid = grn.AmountPaid,
                balanceAmount = grn.BalanceAmount
            }
        });
    }
    [HttpGet]
    public async Task<IActionResult> GetSupplierGrns(int supplierId)
    {
        var grns = await _paymentService.GetOutstandingGrns(supplierId);
        return Json(grns);
    }
    private async Task PopulateDropdowns(int? selectedSupplierId = null)
    {
        ViewBag.Suppliers = new SelectList(await _partyService.GetSuppliers(), "PartyID", "PartyName", selectedSupplierId);
        ViewBag.OutstandingGrns = await _paymentService.GetOutstandingGrns(selectedSupplierId);
    }
}
