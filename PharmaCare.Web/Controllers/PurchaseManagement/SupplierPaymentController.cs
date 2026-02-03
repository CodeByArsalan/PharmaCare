using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Application.Interfaces.Finance;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Domain.Entities.Finance;

namespace PharmaCare.Web.Controllers.PurchaseManagement;

[Authorize]
public class SupplierPaymentController : BaseController
{
    private readonly IPaymentService _paymentService;
    private readonly IPurchaseService _purchaseService;
    private readonly IAccountService _accountService;
    private readonly IPartyService _partyService;

    public SupplierPaymentController(
        IPaymentService paymentService,
        IPurchaseService purchaseService,
        IAccountService accountService,
        IPartyService partyService)
    {
        _paymentService = paymentService;
        _purchaseService = purchaseService;
        _accountService = accountService;
        _partyService = partyService;
    }

    /// Displays list of all supplier payments.
    public async Task<IActionResult> PaymentsIndex()
    {
        var payments = await _paymentService.GetAllSupplierPaymentsAsync();
        return View(payments);
    }

    /// Shows form to make a payment for a GRN.
    public async Task<IActionResult> MakePayment(int stockMainId)
    {
        var grn = await _purchaseService.GetByIdAsync(stockMainId);
        if (grn == null)
        {
            ShowMessage(MessageType.Error, "Purchase not found.");
            return RedirectToAction("PurchasesIndex", "Purchase");
        }

        if (grn.BalanceAmount <= 0)
        {
            ShowMessage(MessageType.Warning, "This purchase is already fully paid.");
            return RedirectToAction("ViewPurchase", "Purchase", new { id = stockMainId });
        }

        await LoadDropdownsAsync();
        ViewBag.GRN = grn;

        return View(new Payment
        {
            StockMain_ID = stockMainId,
            Party_ID = grn.Party_ID ?? 0,
            Amount = grn.BalanceAmount,
            PaymentDate = DateTime.Now,
            PaymentMethod = "Cash"
        });
    }

    /// Processes a payment.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MakePayment(Payment payment)
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
                await _paymentService.CreatePaymentAsync(payment, CurrentUserId);
                ShowMessage(MessageType.Success, "Payment recorded successfully!");
                return RedirectToAction("ViewPurchase", "Purchase", new { id = payment.StockMain_ID });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
            }
        }

        // Reload GRN for display
        var grn = await _purchaseService.GetByIdAsync(payment.StockMain_ID ?? 0);
        ViewBag.GRN = grn;
        await LoadDropdownsAsync();
        return View(payment);
    }

    /// Shows payment details.
    public async Task<IActionResult> ViewPayment(int id)
    {
        var payment = await _paymentService.GetByIdAsync(id);
        if (payment == null)
        {
            ShowMessage(MessageType.Error, "Payment not found.");
            return RedirectToAction(nameof(PaymentsIndex));
        }

        return View(payment);
    }

    /// Gets payment history for a transaction (AJAX).
    [HttpGet]
    public async Task<IActionResult> GetPaymentHistory(int stockMainId)
    {
        var payments = await _paymentService.GetPaymentsByTransactionAsync(stockMainId);
        var result = payments.Select(p => new
        {
            reference = p.Reference,
            date = p.PaymentDate.ToString("dd/MM/yyyy"),
            amount = p.Amount,
            method = p.PaymentMethod,
            account = p.Account?.Name
        }).ToList();

        return Json(result);
    }

    private async Task LoadDropdownsAsync()
    {
        // Load cash/bank accounts for payment (filter by AccountType Code)
        var accounts = await _accountService.GetAllAsync();
        ViewBag.Accounts = new SelectList(
            accounts.Where(a => a.IsActive && (a.AccountType?.Code == "CASH" || a.AccountType?.Code == "BANK")),
            "AccountID",
            "Name"
        );

        // Payment methods
        ViewBag.PaymentMethods = new SelectList(new[] { "Cash", "Bank", "Cheque" });
    }
}
