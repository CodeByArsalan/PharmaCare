using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Application.Interfaces.Finance;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Domain.Entities.Finance;
using PharmaCare.Web.Filters;
using PharmaCare.Web.Utilities;

namespace PharmaCare.Web.Controllers.PurchaseManagement;

[Authorize]
public class SupplierPaymentController : BaseController
{
    private readonly IPaymentService _paymentService;
    private readonly IPurchaseService _purchaseService;
    private readonly IAccountService _accountService;
    private readonly IPartyService _partyService;
    private readonly IPurchaseReturnService _purchaseReturnService;
    private readonly ISupplierCreditNoteService _creditNoteService;

    public SupplierPaymentController(
        IPaymentService paymentService,
        IPurchaseService purchaseService,
        IAccountService accountService,
        IPartyService partyService,
        IPurchaseReturnService purchaseReturnService,
        ISupplierCreditNoteService creditNoteService)
    {
        _paymentService = paymentService;
        _purchaseService = purchaseService;
        _accountService = accountService;
        _partyService = partyService;
        _purchaseReturnService = purchaseReturnService;
        _creditNoteService = creditNoteService;
    }

    /// Displays list of GRNs with payment information.
    public async Task<IActionResult> PaymentsIndex(int? supplierId, string? paymentStatus, DateTime? fromDate, DateTime? toDate, int page = 1, int pageSize = 25)
    {
        page = NormalizePage(page);
        pageSize = NormalizePageSize(pageSize);
        var pagedResult = await _paymentService.GetPagedPendingGrnsAsync(supplierId, fromDate, toDate, paymentStatus, page, pageSize);

        ViewBag.Suppliers = await GetPartySelectListAsync(_partyService, "Supplier", supplierId);
        
        ViewBag.SelectedSupplier = supplierId;
        ViewBag.SelectedStatus = paymentStatus ?? "All";
        ViewBag.FromDate = fromDate;
        ViewBag.ToDate = toDate;

        return View(pagedResult);
    }

    /// Shows form to make a payment for a GRN.
    public async Task<IActionResult> MakePayment(string stockMainId)
    {
        int id = Utility.DecryptId(stockMainId);
        if (id == 0)
        {
             ShowMessage(MessageType.Error, "Invalid Purchase ID.");
             return RedirectToAction("PurchasesIndex", "Purchase");
        }

        var grn = await _purchaseService.GetByIdAsync(id);
        if (grn == null)
        {
            ShowMessage(MessageType.Error, "Purchase not found.");
            return RedirectToAction("PurchasesIndex", "Purchase");
        }

        await RefreshGrnOutstandingAsync(grn);

        if (!string.Equals(grn.Status, "Approved", StringComparison.OrdinalIgnoreCase))
        {
            ShowMessage(MessageType.Warning, "Payments can only be made against approved purchases.");
            return RedirectToAction(nameof(PaymentsIndex));
        }

        if (grn.BalanceAmount <= 0)
        {
            ShowMessage(MessageType.Warning, "This purchase is already fully paid.");
            return RedirectToAction("ViewPurchase", "Purchase", new { id = stockMainId });
        }

        ViewBag.GRN = grn;

        return View(new Payment
        {
            StockMain_ID = id,
            Party_ID = grn.Party_ID ?? 0,
            Amount = grn.BalanceAmount,
            PaymentDate = AppTime.Now,
            PaymentMethod = "Cash"
        });
    }

    /// Processes a payment.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [LinkedToPage("SupplierPayment", "PaymentsIndex", PermissionType = "create")]
    public async Task<IActionResult> MakePayment(
        [Bind("StockMain_ID,Party_ID,Amount,PaymentDate,PaymentMethod,Account_ID,ChequeNo,ChequeDate,Remarks")] Payment payment)
    {
        var grn = await _purchaseService.GetByIdAsync(payment.StockMain_ID ?? 0);
        if (grn == null)
        {
            ShowMessage(MessageType.Error, "Purchase not found.");
            return RedirectToAction(nameof(PaymentsIndex));
        }

        await RefreshGrnOutstandingAsync(grn);

        if (!string.Equals(grn.Status, "Approved", StringComparison.OrdinalIgnoreCase))
        {
            ShowMessage(MessageType.Warning, "Payments can only be made against approved purchases.");
            return RedirectToAction(nameof(PaymentsIndex));
        }

        if (grn.BalanceAmount <= 0)
        {
            ShowMessage(MessageType.Warning, "This purchase is already fully paid.");
            return RedirectToAction(nameof(PaymentsIndex));
        }

        // Overpayment guard — use ShowMessage + redirect so the user lands back on the payment form
        if (payment.Amount > grn.BalanceAmount)
        {
            var encryptedId = grn.StockMainID.EncryptId();
            ShowMessage(MessageType.Error, $"Payment amount ({payment.Amount:N2}) cannot exceed the outstanding balance ({grn.BalanceAmount:N2}).");
            return RedirectToAction(nameof(MakePayment), new { stockMainId = encryptedId });
        }

        if (!grn.Party_ID.HasValue || grn.Party_ID.Value <= 0)
        {
            ShowMessage(MessageType.Error, "Selected purchase is not linked to a supplier.");
            return RedirectToAction(nameof(PaymentsIndex));
        }

        payment.StockMain_ID = grn.StockMainID;
        payment.Party_ID = grn.Party_ID.Value;

        CleanNavigationModelState("Party", "StockMain", "Account", "Voucher", "PaymentType", "Reference");

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
                ModelState.AddModelError("", SafeErrorMessage(ex, "MakePayment"));
            }
        }

        ViewBag.GRN = grn;
        return View(payment);
    }

    /// Shows payment details.
    public async Task<IActionResult> ViewPayment(string id)
    {
        int paymentId = Utility.DecryptId(id);
        if (paymentId == 0)
        {
            ShowMessage(MessageType.Error, "Invalid Payment ID.");
            return RedirectToAction(nameof(PaymentsIndex));
        }

        var payment = await _paymentService.GetByIdAsync(paymentId);
        if (payment == null)
        {
            ShowMessage(MessageType.Error, "Payment not found.");
            return RedirectToAction(nameof(PaymentsIndex));
        }

        return View(payment);
    }

    /// Gets payment history for a transaction (AJAX).
    [HttpGet]
    public async Task<IActionResult> GetPaymentHistory(string stockMainId)
    {
        int id = Utility.DecryptId(stockMainId);
        var payments = await _paymentService.GetPaymentsByTransactionAsync(id);
        
        var orderedPayments = payments.OrderByDescending(p => p.PaymentID).ToList();
        int sNo = 1;
        var result = orderedPayments.Select(p => new
        {
            sNo = sNo++,
            id = p.PaymentID.EncryptId(),
            reference = p.Reference,
            date = p.PaymentDate.ToString("dd/MM/yyyy"),
            amount = p.Amount,
            method = p.PaymentMethod,
            account = p.Account?.Name,
            isVoided = p.IsVoided,
            voidReason = p.VoidReason
        }).ToList();

        return Json(result);
    }

    /// Gets accounts by payment method (AJAX).
    [HttpGet]
    [LinkedToPage("SupplierPayment", "PaymentsIndex")]
    public async Task<IActionResult> GetAccountsByMethod(string method)
    {
        return await GetAccountsByMethodAsync(_accountService, method);
    }

    /// Legacy alias for GetAccountsByMethod.
    [HttpGet]
    [LinkedToPage("SupplierPayment", "PaymentsIndex")]
    public async Task<IActionResult> GetAccountsByType(string method, int? typeId)
    {
        // If method is provided, use it. Otherwise fall back to typeId mapping for legacy support.
        if (string.IsNullOrEmpty(method) && typeId.HasValue)
        {
            method = typeId == 1 ? "Cash" : "Bank";
        }
        return await GetAccountsByMethod(method ?? "Cash");
    }

    /// Voids a supplier payment.
    [HttpPost]
    [ValidateAntiForgeryToken]
    // Voiding a payment reverses cash out of the ledger — gated on "delete", not the page's
    // default "view".
    [LinkedToPage("SupplierPayment", "PaymentsIndex", PermissionType = "delete")]
    public async Task<IActionResult> VoidPayment(string id, string reason)
    {
        int paymentId = Utility.DecryptId(id);
        if (paymentId == 0)
        {
            ShowMessage(MessageType.Error, "Invalid Payment ID.");
            return RedirectToAction(nameof(PaymentsIndex));
        }

        var voidReason = string.IsNullOrWhiteSpace(reason) ? "Voided by user" : reason.Trim();
        if (voidReason.Length > MaxVoidReasonLength)
        {
            ShowMessage(MessageType.Error,
                $"The reason is too long — please keep it to {MaxVoidReasonLength} characters or fewer.");
            return RedirectToAction(nameof(PaymentsIndex));
        }

        try
        {
            await _paymentService.VoidPaymentAsync(paymentId, voidReason, CurrentUserId);
            ShowMessage(MessageType.Success, "Payment voided successfully!");
        }
        catch (Exception ex)
        {
            ShowMessage(MessageType.Error, SafeErrorMessage(ex, "VoidPayment"));
        }

        return RedirectToAction(nameof(PaymentsIndex));
    }

    /// Shows the supplier advance refund form.
    [LinkedToPage("SupplierPayment", "PaymentsIndex", PermissionType = "create")]
    public async Task<IActionResult> RefundAdvance(int? supplierId)
    {
        ViewBag.Suppliers = await GetPartySelectListAsync(_partyService, "Supplier", supplierId);
        ViewBag.SelectedSupplier = supplierId;

        decimal advance = 0;
        if (supplierId.HasValue && supplierId.Value > 0)
        {
            advance = await _purchaseService.GetSupplierAdvanceAsync(supplierId.Value);
        }
        ViewBag.AvailableAdvance = advance;

        return View(new Payment
        {
            Party_ID = supplierId ?? 0,
            Amount = advance,
            PaymentDate = AppTime.Now,
            PaymentMethod = "Cash"
        });
    }

    /// Returns a supplier's available on-account advance (AJAX).
    [HttpGet]
    [LinkedToPage("SupplierPayment", "PaymentsIndex")]
    public async Task<IActionResult> GetSupplierAdvance(int supplierId)
    {
        var advance = supplierId > 0 ? await _purchaseService.GetSupplierAdvanceAsync(supplierId) : 0;
        return Json(new { advance });
    }

    /// Processes a supplier advance refund.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [LinkedToPage("SupplierPayment", "PaymentsIndex", PermissionType = "create")]
    public async Task<IActionResult> RefundAdvance(
        [Bind("Party_ID,PaymentDate,Amount,PaymentMethod,Account_ID,Remarks")] Payment payment)
    {
        CleanNavigationModelState("Party", "StockMain", "Account", "Voucher", "PaymentType", "Reference");

        if (payment.Party_ID <= 0)
        {
            ModelState.AddModelError(nameof(payment.Party_ID), "Supplier is required.");
        }
        if (payment.Amount <= 0)
        {
            ModelState.AddModelError(nameof(payment.Amount), "Refund amount must be greater than zero.");
        }

        if (ModelState.IsValid)
        {
            try
            {
                await _paymentService.CreateSupplierRefundAsync(payment, CurrentUserId);
                ShowMessage(MessageType.Success, "Supplier advance refunded successfully!");
                return RedirectToAction(nameof(PaymentsIndex));
            }
            catch (Exception ex)
            {
                ShowMessage(MessageType.Error, SafeErrorMessage(ex, "RefundAdvance"));
            }
        }

        ViewBag.Suppliers = await GetPartySelectListAsync(_partyService, "Supplier", payment.Party_ID);
        ViewBag.SelectedSupplier = payment.Party_ID;
        ViewBag.AvailableAdvance = payment.Party_ID > 0
            ? await _purchaseService.GetSupplierAdvanceAsync(payment.Party_ID)
            : 0m;
        return View(payment);
    }

    private async Task RefreshGrnOutstandingAsync(Domain.Entities.Transactions.StockMain grn)
    {
        if (!grn.Party_ID.HasValue || grn.Party_ID.Value <= 0)
        {
            return;
        }

        var pendingForSupplier = await _paymentService.GetPendingGrnsAsync(grn.Party_ID.Value);
        var refreshedGrn = pendingForSupplier.FirstOrDefault(x => x.StockMainID == grn.StockMainID);
        if (refreshedGrn == null)
        {
            grn.BalanceAmount = 0;
            grn.PaymentStatus = "Paid";
            return;
        }

        grn.BalanceAmount = refreshedGrn.BalanceAmount;
        grn.PaymentStatus = refreshedGrn.PaymentStatus;
    }


    /// Displays Supplier Reconciliation tool.
    [LinkedToPage("SupplierPayment", "PaymentsIndex")]
    public async Task<IActionResult> SupplierReconciliation(int? supplierId)
    {
        ViewBag.Suppliers = await GetPartySelectListAsync(_partyService, "Supplier", supplierId);

        ViewBag.SelectedSupplier = supplierId;

        if (!supplierId.HasValue)
        {
            ViewBag.PendingGrns = new List<dynamic>();
            ViewBag.AvailableCredits = new List<dynamic>();
            return View();
        }

        var supplierGrns = await _paymentService.GetPendingGrnsAsync(supplierId.Value, false);
        ViewBag.PendingGrns = supplierGrns;

        var availableCredits = new List<dynamic>();


        // 2. Supplier Credit Notes
        var openCreditNotes = await _creditNoteService.GetOpenAsync(supplierId.Value);
        var creditNotes = openCreditNotes
            .Select(c => new {
                IdType = "CreditNote",
                Id = c.SupplierCreditNoteID.EncryptId(),
                Reference = c.CreditNoteNo,
                Date = c.CreditDate,
                TotalAmount = c.TotalAmount,
                AvailableAmount = c.BalanceAmount
            })
            .ToList();

        availableCredits.AddRange(creditNotes);

        ViewBag.AvailableCredits = availableCredits.OrderBy(x => x.Date).ToList();

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // Applying a credit note changes what is owed on a posted GRN — a write, not a view.
    [LinkedToPage("SupplierPayment", "PaymentsIndex", PermissionType = "edit")]
    public async Task<IActionResult> ApplyCredit(string creditType, string creditId, string grnId, decimal amount)
    {
        try
        {
            int cId = Utility.DecryptId(creditId);
            int gId = Utility.DecryptId(grnId);

            if (cId == 0 || gId == 0)
                throw new InvalidOperationException("Invalid IDs provided.");

            if (creditType == "CreditNote")
            {
                await _creditNoteService.ApplyToGrnAsync(cId, gId, amount, CurrentUserId);
                ShowMessage(MessageType.Success, "Credit Note applied successfully.");
            }
            else
            {
                throw new InvalidOperationException("Unknown credit type.");
            }
        }
        catch (Exception ex)
        {
            ShowMessage(MessageType.Error, SafeErrorMessage(ex, "ApplyCredit"));
        }

        // Redirect back to the referring page so the selected supplier is preserved — but only if
        // it points inside this application. The Referer header is attacker-controlled, so echoing
        // it unchecked turns this endpoint into an open redirect.
        var referer = Request.Headers["Referer"].ToString();
        if (!string.IsNullOrEmpty(referer) && Url.IsLocalUrl(referer))
            return Redirect(referer);

        return RedirectToAction(nameof(SupplierReconciliation));
    }
}
