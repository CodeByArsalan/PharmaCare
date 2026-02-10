using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Application.Interfaces.Finance;
using PharmaCare.Domain.Entities.Finance;

namespace PharmaCare.Web.Controllers.SalesManagement;

public class CustomerPaymentController : BaseController
{
    private readonly ICustomerPaymentService _customerPaymentService;
    private readonly IAccountService _accountService;
    private readonly IPartyService _partyService;

    public CustomerPaymentController(
        ICustomerPaymentService customerPaymentService,
        IAccountService accountService,
        IPartyService partyService)
    {
        _customerPaymentService = customerPaymentService;
        _accountService = accountService;
        _partyService = partyService;
    }

    /// Displays list of all customer receipts.
    public async Task<IActionResult> ReceiptsIndex()
    {
        var receipts = await _customerPaymentService.GetAllCustomerReceiptsAsync();
        return View(receipts);
    }

    /// Shows pending sales for receipt collection.
    public async Task<IActionResult> PendingSales()
    {
        var pendingSales = await _customerPaymentService.GetPendingSalesAsync();
        return View(pendingSales);
    }

    /// Shows form to receive payment for a sale.
    public async Task<IActionResult> ReceivePayment(int stockMainId)
    {
        var pendingSales = await _customerPaymentService.GetPendingSalesAsync();
        var sale = pendingSales.FirstOrDefault(s => s.StockMainID == stockMainId);
        
        if (sale == null)
        {
            ShowMessage(MessageType.Error, "Sale not found or already fully paid.");
            return RedirectToAction(nameof(PendingSales));
        }

        await LoadDropdownsAsync();
        ViewBag.Sale = sale;
        ViewBag.IsWalkingCustomer = sale.Party_ID == null;

        return View(new Payment
        {
            StockMain_ID = stockMainId,
            Party_ID = sale.Party_ID ?? 0,
            Amount = sale.BalanceAmount,
            PaymentDate = DateTime.Now,
            PaymentMethod = "Cash"
        });
    }

    /// Processes a receipt.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReceivePayment(Payment payment, bool isWalkingCustomer = false)
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
                if (isWalkingCustomer)
                {
                    await _customerPaymentService.CreateWalkingCustomerReceiptAsync(payment, CurrentUserId);
                }
                else
                {
                    await _customerPaymentService.CreateReceiptAsync(payment, CurrentUserId);
                }
                
                ShowMessage(MessageType.Success, "Payment received successfully!");
                return RedirectToAction(nameof(ReceiptsIndex));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
            }
        }

        // Reload Sale for display
        var pendingSales = await _customerPaymentService.GetPendingSalesAsync();
        var sale = pendingSales.FirstOrDefault(s => s.StockMainID == payment.StockMain_ID);
        ViewBag.Sale = sale;
        ViewBag.IsWalkingCustomer = isWalkingCustomer;
        await LoadDropdownsAsync();
        return View(payment);
    }

    /// Shows receipt details.
    public async Task<IActionResult> ViewReceipt(int id)
    {
        var receipt = await _customerPaymentService.GetByIdAsync(id);
        if (receipt == null)
        {
            ShowMessage(MessageType.Error, "Receipt not found.");
            return RedirectToAction(nameof(ReceiptsIndex));
        }

        return View(receipt);
    }

    /// Gets receipt history for a transaction (AJAX).
    [HttpGet]
    public async Task<IActionResult> GetReceiptHistory(int stockMainId)
    {
        var receipts = await _customerPaymentService.GetReceiptsByTransactionAsync(stockMainId);
        var result = receipts.Select(p => new
        {
            reference = p.Reference,
            date = p.PaymentDate.ToString("dd/MM/yyyy"),
            amount = p.Amount,
            method = p.PaymentMethod,
            account = p.Account?.Name
        }).ToList();

        return Json(result);
    }

    /// Gets accounts by type ID (AJAX).
    [HttpGet]
    public async Task<IActionResult> GetAccountsByType(int typeId)
    {
        var accounts = await _accountService.GetAllAsync();
        var filteredAccounts = accounts
            .Where(a => a.IsActive && a.AccountType_ID == typeId)
            .Select(a => new { id = a.AccountID, name = a.Name })
            .ToList();

        return Json(filteredAccounts);
    }

    /// Displays list of all customer refunds.
    public async Task<IActionResult> RefundsIndex()
    {
        var refunds = await _customerPaymentService.GetAllRefundsAsync();
        return View(refunds);
    }

    /// Shows form to create a customer refund.
    public async Task<IActionResult> Refund()
    {
        await LoadDropdownsAsync();

        var parties = await _partyService.GetAllAsync();
        ViewBag.Customers = new SelectList(
            parties.Where(p => p.IsActive && p.PartyType == "Customer"),
            "PartyID",
            "Name"
        );

        return View(new Payment
        {
            PaymentDate = DateTime.Now,
            PaymentMethod = "Cash"
        });
    }

    /// Processes a customer refund.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Refund(Payment payment)
    {
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
                await _customerPaymentService.CreateRefundAsync(payment, CurrentUserId);
                ShowMessage(MessageType.Success, "Customer refund recorded successfully!");
                return RedirectToAction(nameof(RefundsIndex));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
            }
        }

        await LoadDropdownsAsync();

        var parties = await _partyService.GetAllAsync();
        ViewBag.Customers = new SelectList(
            parties.Where(p => p.IsActive && p.PartyType == "Customer"),
            "PartyID",
            "Name"
        );

        return View(payment);
    }

    private async Task LoadDropdownsAsync()
    {
        // Load cash/bank accounts for receipt (filter by AccountType Code)
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
