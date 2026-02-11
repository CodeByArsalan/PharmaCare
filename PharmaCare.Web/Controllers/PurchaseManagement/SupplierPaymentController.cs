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

    /// Displays list of GRNs with payment information.
    public async Task<IActionResult> PaymentsIndex(int? supplierId, string? paymentStatus, DateTime? fromDate, DateTime? toDate)
    {
        // Get all GRNs (purchases)
        var grns = await _purchaseService.GetAllAsync();
        var grnList = grns.ToList();

        // Apply filters
        if (supplierId.HasValue)
        {
            grnList = grnList.Where(g => g.Party_ID == supplierId.Value).ToList();
        }

        if (!string.IsNullOrEmpty(paymentStatus) && paymentStatus != "All")
        {
            grnList = grnList.Where(g => g.PaymentStatus == paymentStatus).ToList();
        }

        if (fromDate.HasValue)
        {
            grnList = grnList.Where(g => g.TransactionDate >= fromDate.Value).ToList();
        }

        if (toDate.HasValue)
        {
            grnList = grnList.Where(g => g.TransactionDate <= toDate.Value).ToList();
        }

        // ViewBag.Suppliers removed - use IComboboxRepository in View
        // ViewBag.PaymentStatuses removed - use IComboboxRepository in View

        // Preserve filter values (keep these as they are simple values)
        ViewBag.SelectedSupplier = supplierId;
        ViewBag.SelectedStatus = paymentStatus ?? "All";
        ViewBag.FromDate = fromDate;
        ViewBag.ToDate = toDate ?? DateTime.Today;

        return View(grnList);
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

        // await LoadDropdownsAsync(); // Removed
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
                return RedirectToAction(nameof(PaymentsIndex));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
            }
        }

        // Reload GRN for display
        var grn = await _purchaseService.GetByIdAsync(payment.StockMain_ID ?? 0);
        ViewBag.GRN = grn;
        // await LoadDropdownsAsync(); // Removed
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
        
        // Order by PaymentID descending (newest first) and add serial number
        var orderedPayments = payments.OrderByDescending(p => p.PaymentID).ToList();
        int sNo = 1;
        var result = orderedPayments.Select(p => new
        {
            sNo = sNo++,
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

    /// Displays list of advance payments.
    public async Task<IActionResult> AdvancePaymentsIndex()
    {
        var advancePayments = await _paymentService.GetAdvancePaymentsAsync();
        return View(advancePayments);
    }

    /// Shows form to make an advance payment to a supplier.
    public IActionResult AdvancePayment()
    {
        // await LoadDropdownsAsync(); // Removed

        // Supplier dropdown removed - use IComboboxRepository

        return View(new Payment
        {
            PaymentDate = DateTime.Now,
            PaymentMethod = "Cash"
        });
    }

    /// Processes an advance payment.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdvancePayment(Payment payment)
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
                await _paymentService.CreateAdvancePaymentAsync(payment, CurrentUserId);
                ShowMessage(MessageType.Success, "Advance payment recorded successfully!");
                return RedirectToAction(nameof(AdvancePaymentsIndex));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
            }
        }

        // await LoadDropdownsAsync(); // Removed

        // Supplier dropdown removed - use IComboboxRepository

        return View(payment);
    }

    // private async Task LoadDropdownsAsync() { ... } // Removed
}
