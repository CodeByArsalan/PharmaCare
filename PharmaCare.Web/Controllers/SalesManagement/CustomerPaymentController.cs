using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Application.Interfaces.Finance;
using PharmaCare.Domain.Entities.Finance;
using PharmaCare.Web.Filters;
using PharmaCare.Web.Utilities;

namespace PharmaCare.Web.Controllers.SalesManagement;

public class CustomerPaymentController : BaseController
{
    private readonly ICustomerPaymentService _customerPaymentService;
    private readonly IAccountService _accountService;

    public CustomerPaymentController(
        ICustomerPaymentService customerPaymentService,
        IAccountService accountService)
    {
        _customerPaymentService = customerPaymentService;
        _accountService = accountService;
    }

    /// Displays list of all customer receipts.
    [LinkedToPage("CustomerPayment", "ReceiptsIndex")]
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
    public async Task<IActionResult> ReceivePayment(string stockMainId)
    {
        int id = Utility.DecryptId(stockMainId);
        if (id == 0)
        {
             ShowMessage(MessageType.Error, "Invalid Sale ID.");
             return RedirectToAction(nameof(PendingSales));
        }

        var pendingSales = await _customerPaymentService.GetPendingSalesAsync();
        var sale = pendingSales.FirstOrDefault(s => s.StockMainID == id);
        
        if (sale == null)
        {
            ShowMessage(MessageType.Error, "Sale not found or already fully paid.");
            return RedirectToAction(nameof(PendingSales));
        }

        // await LoadDropdownsAsync(); // Removed
        ViewBag.Sale = sale;

        if (!sale.Party_ID.HasValue || sale.Party_ID.Value <= 0)
        {
            ShowMessage(MessageType.Error, "This sale is not linked to a customer. Please update the sale first.");
            return RedirectToAction(nameof(PendingSales));
        }

        return View(new Payment
        {
            StockMain_ID = id,
            Party_ID = sale.Party_ID.Value,
            Amount = sale.BalanceAmount,
            PaymentDate = DateTime.Now,
            PaymentMethod = "Cash"
        });
    }

    /// Processes a receipt.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReceivePayment(Payment payment)
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
                await _customerPaymentService.CreateReceiptAsync(payment, CurrentUserId);
                
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
        // await LoadDropdownsAsync(); // Removed
        return View(payment);
    }

    /// Shows receipt details.
    public async Task<IActionResult> ViewReceipt(string id)
    {
        int receiptId = Utility.DecryptId(id);
        if (receiptId == 0)
        {
            ShowMessage(MessageType.Error, "Invalid Receipt ID.");
            return RedirectToAction(nameof(ReceiptsIndex));
        }

        var receipt = await _customerPaymentService.GetByIdAsync(receiptId);
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
    [LinkedToPage("CustomerPayment", "ReceiptsIndex")]
    public async Task<IActionResult> RefundsIndex()
    {
        var refunds = await _customerPaymentService.GetAllRefundsAsync();
        return View(refunds);
    }

    [LinkedToPage("CustomerPayment", "ReceiptsIndex")]
    public async Task<IActionResult> CreditNotesIndex(int? customerId)
    {
        var creditNotes = await _customerPaymentService.GetOpenCreditNotesAsync(customerId);
        ViewBag.SelectedCustomerId = customerId;
        return View(creditNotes);
    }

    [LinkedToPage("CustomerPayment", "ReceiptsIndex")]
    public async Task<IActionResult> Reconciliation(int? customerId)
    {
        var vm = await _customerPaymentService.GetCustomerReconciliationAsync(customerId);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [LinkedToPage("CustomerPayment", "ReceiptsIndex", PermissionType = "create")]
    public async Task<IActionResult> ApplyCreditNote(int creditNoteId, int saleId, decimal amount, int? customerId)
    {
        try
        {
            await _customerPaymentService.ApplyCreditNoteAsync(creditNoteId, saleId, amount, CurrentUserId);
            ShowMessage(MessageType.Success, "Credit note applied successfully.");
        }
        catch (Exception ex)
        {
            ShowMessage(MessageType.Error, ex.Message);
        }

        return RedirectToAction(nameof(Reconciliation), new { customerId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [LinkedToPage("CustomerPayment", "ReceiptsIndex", PermissionType = "delete")]
    public async Task<IActionResult> VoidReceipt(string id, string voidReason)
    {
        var paymentId = Utility.DecryptId(id);
        if (paymentId <= 0)
        {
            ShowMessage(MessageType.Error, "Invalid receipt ID.");
            return RedirectToAction(nameof(ReceiptsIndex));
        }

        try
        {
            var result = await _customerPaymentService.VoidReceiptAsync(paymentId, voidReason, CurrentUserId);
            ShowMessage(result ? MessageType.Success : MessageType.Warning, result ? "Receipt voided successfully." : "Receipt is already voided.");
        }
        catch (Exception ex)
        {
            ShowMessage(MessageType.Error, ex.Message);
        }

        return RedirectToAction(nameof(ReceiptsIndex));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [LinkedToPage("CustomerPayment", "ReceiptsIndex", PermissionType = "delete")]
    public async Task<IActionResult> VoidRefund(string id, string voidReason)
    {
        var paymentId = Utility.DecryptId(id);
        if (paymentId <= 0)
        {
            ShowMessage(MessageType.Error, "Invalid refund ID.");
            return RedirectToAction(nameof(RefundsIndex));
        }

        try
        {
            var result = await _customerPaymentService.VoidRefundAsync(paymentId, voidReason, CurrentUserId);
            ShowMessage(result ? MessageType.Success : MessageType.Warning, result ? "Refund voided successfully." : "Refund is already voided.");
        }
        catch (Exception ex)
        {
            ShowMessage(MessageType.Error, ex.Message);
        }

        return RedirectToAction(nameof(RefundsIndex));
    }

    /// Shows form to create a customer refund.
    public IActionResult Refund()
    {
        // await LoadDropdownsAsync(); // Removed

        // Custom dropdown loading removed - use IComboboxRepository in View
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

        // await LoadDropdownsAsync(); // Removed

        // Custom dropdown loading removed
        return View(payment);
    }

    // private async Task LoadDropdownsAsync() { ... } // Removed
}
