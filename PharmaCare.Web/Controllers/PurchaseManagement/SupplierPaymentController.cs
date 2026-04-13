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

    public SupplierPaymentController(
        IPaymentService paymentService,
        IPurchaseService purchaseService,
        IAccountService accountService,
        IPartyService partyService,
        IPurchaseReturnService purchaseReturnService)
    {
        _paymentService = paymentService;
        _purchaseService = purchaseService;
        _accountService = accountService;
        _partyService = partyService;
        _purchaseReturnService = purchaseReturnService;
    }

    /// Displays list of GRNs with payment information.
    public async Task<IActionResult> PaymentsIndex(int? supplierId, string? paymentStatus, DateTime? fromDate, DateTime? toDate) 
    {
        bool includePaid = paymentStatus == "Paid" || paymentStatus == "All";
        var grns = await _paymentService.GetPendingGrnsAsync(supplierId, includePaid);
        var grnList = grns.ToList();

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

        ViewBag.SelectedSupplier = supplierId;
        ViewBag.SelectedStatus = paymentStatus ?? "All";
        ViewBag.FromDate = fromDate ?? DateTime.Today;
        ViewBag.ToDate = toDate ?? DateTime.Today;

        return View(grnList);
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
            PaymentDate = DateTime.Now,
            PaymentMethod = "Cash"
        });
    }

    /// Processes a payment.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MakePayment(Payment payment)
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

        if (!grn.Party_ID.HasValue || grn.Party_ID.Value <= 0)
        {
            ModelState.AddModelError("", "Selected purchase is not linked to a supplier.");
        }
        else
        {
            payment.StockMain_ID = grn.StockMainID;
            payment.Party_ID = grn.Party_ID.Value;
        }

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
        var accounts = await _accountService.GetAccountsByMethodAsync(method);
        var result = accounts
            .Select(a => new { id = a.AccountID, name = a.Name })
            .ToList();

        return Json(result);
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

    /// Singular alias to handle potential typos in views.
    [HttpGet]
    [LinkedToPage("SupplierPayment", "PaymentsIndex")]
    public async Task<IActionResult> GetAccountByType(string method, int? typeId)
    {
        return await GetAccountsByType(method, typeId);
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

        return View(payment);
    }

    /// Voids a supplier payment.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [LinkedToPage("SupplierPayment", "PaymentsIndex")]
    public async Task<IActionResult> VoidPayment(string id, string reason)
    {
        int paymentId = Utility.DecryptId(id);
        if (paymentId == 0)
        {
            ShowMessage(MessageType.Error, "Invalid Payment ID.");
            return RedirectToAction(nameof(PaymentsIndex));
        }

        try
        {
            await _paymentService.VoidPaymentAsync(paymentId, reason ?? "Voided by user", CurrentUserId);
            ShowMessage(MessageType.Success, "Payment voided successfully!");
        }
        catch (Exception ex)
        {
            ShowMessage(MessageType.Error, ex.Message);
        }

        return RedirectToAction(nameof(PaymentsIndex));
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

    /// Displays a supplier ledger/statement.
    [LinkedToPage("SupplierPayment", "PaymentsIndex")]
    public async Task<IActionResult> SupplierLedger(int? supplierId, DateTime? fromDate, DateTime? toDate)
    {
        var parties = await _partyService.GetAllAsync();
        ViewBag.Suppliers = new SelectList(
            parties.Where(p => p.IsActive && (p.PartyType == "Supplier" || p.PartyType == "Both")),
            "PartyID", "Name", supplierId
        );

        ViewBag.SelectedSupplier = supplierId;
        ViewBag.FromDate = fromDate;
        ViewBag.ToDate = toDate ?? DateTime.Now;

        if (!supplierId.HasValue)
        {
            ViewBag.LedgerEntries = new List<dynamic>();
            ViewBag.SupplierName = "";
            return View();
        }

        var supplier = parties.FirstOrDefault(p => p.PartyID == supplierId.Value);
        ViewBag.SupplierName = supplier?.Name ?? "Unknown";

        var allGrns = await _purchaseService.GetAllAsync();
        var supplierGrns = allGrns
            .Where(g => g.Party_ID == supplierId.Value && g.Status != "Void")
            .ToList();

        var allReturns = await _purchaseReturnService.GetAllAsync();
        var supplierReturns = allReturns
            .Where(r => r.Party_ID == supplierId.Value && r.Status != "Void")
            .ToList();

        var allPayments = await _paymentService.GetPaymentsByPartyAsync(supplierId.Value);
        var supplierPayments = allPayments.Where(p => !p.IsVoided).ToList();

        // Calculate opening balance if fromDate is specified
        decimal openingBalance = supplier?.OpeningBalance ?? 0;
        if (fromDate.HasValue)
        {
            decimal priorPurchases = supplierGrns.Where(g => g.TransactionDate < fromDate.Value).Sum(g => g.TotalAmount);
            decimal priorReturns = supplierReturns.Where(r => r.TransactionDate < fromDate.Value).Sum(r => r.TotalAmount);
            decimal priorPayments = supplierPayments.Where(p => p.PaymentDate < fromDate.Value).Sum(p => p.Amount);
            
            // For supplier: Purchases increase payable (Credit). Returns/Payments decrease payable (Debit).
            // Opening Balance = Base Opening + Prior Purchases - Prior Returns - Prior Payments
            openingBalance += priorPurchases - priorReturns - priorPayments;
        }

        // Build ledger entries for the period
        var entries = new List<LedgerEntry>();

        if (fromDate.HasValue || openingBalance != 0)
        {
            entries.Add(new LedgerEntry
            {
                Date = fromDate ?? DateTime.MinValue,
                Reference = "Opening Balance",
                Type = "-",
                TypeBadge = "secondary",
                Debit = 0,
                Credit = 0,
                Balance = openingBalance,
                Remarks = "Balance carried forward",
                EncryptedId = "",
                Source = "OpeningBalance"
            });
        }

        // GRNs → Credit (increase payable to supplier)
        foreach (var grn in supplierGrns)
        {
            if (fromDate.HasValue && grn.TransactionDate < fromDate.Value) continue;
            if (toDate.HasValue && grn.TransactionDate > toDate.Value.AddDays(1)) continue;

            entries.Add(new LedgerEntry
            {
                Date = grn.TransactionDate,
                Reference = grn.TransactionNo,
                Type = "Purchase (GRN)",
                TypeBadge = "primary",
                Debit = 0,               // Changed: Purchases are Credit to Supplier Payable
                Credit = grn.TotalAmount,
                Remarks = grn.Remarks,
                EncryptedId = grn.StockMainID.EncryptId(),
                Source = "Purchase"
            });
        }

        // Purchase Returns → Debit (reduces payable to supplier) 
        foreach (var prtn in supplierReturns)
        {
            if (fromDate.HasValue && prtn.TransactionDate < fromDate.Value) continue;
            if (toDate.HasValue && prtn.TransactionDate > toDate.Value.AddDays(1)) continue;

            entries.Add(new LedgerEntry
            {
                Date = prtn.TransactionDate,
                Reference = prtn.TransactionNo,
                Type = "Purchase Return",
                TypeBadge = "warning",
                Debit = prtn.TotalAmount, // Returns reduce what we owe
                Credit = 0,               
                Remarks = prtn.Remarks,
                EncryptedId = prtn.StockMainID.EncryptId(),
                Source = "PurchaseReturn"
            });
        }

        // Payments → Debit (reduces payable to supplier)
        foreach (var payment in supplierPayments)
        {
            if (fromDate.HasValue && payment.PaymentDate < fromDate.Value) continue;
            if (toDate.HasValue && payment.PaymentDate > toDate.Value.AddDays(1)) continue;

            var payType = payment.StockMain_ID.HasValue ? "Payment" : "Advance Payment";
            entries.Add(new LedgerEntry
            {
                Date = payment.PaymentDate,
                Reference = payment.Reference ?? "-",
                Type = payType,
                TypeBadge = "success",
                Debit = payment.Amount,  // Payments reduce what we owe
                Credit = 0,              
                Remarks = payment.Remarks,
                EncryptedId = payment.PaymentID.EncryptId(),
                Source = "Payment"
            });
        }

        // Separate opening balance from period transactions so we sort them correctly
        var openingEntry = entries.FirstOrDefault(e => e.Source == "OpeningBalance");
        var periodEntries = entries.Where(e => e.Source != "OpeningBalance")
                                   .OrderBy(e => e.Date)
                                   .ThenBy(e => e.Reference)
                                   .ToList();

        // Calculate running balance using Payable Logic (Credit increases Payable, Debit reduces it)
        decimal balance = openingBalance;
        if (openingEntry != null)
        {
            openingEntry.Balance = balance;
        }

        foreach (var entry in periodEntries)
        {
            balance += entry.Credit - entry.Debit;
            entry.Balance = balance;
        }

        var finalEntries = new List<LedgerEntry>();
        if (openingEntry != null) finalEntries.Add(openingEntry);
        finalEntries.AddRange(periodEntries);

        ViewBag.LedgerEntries = finalEntries;
        // Total Debit and Credit calculation for the period (excludes Opening Balance)
        ViewBag.TotalDebit = periodEntries.Sum(e => e.Debit);
        ViewBag.TotalCredit = periodEntries.Sum(e => e.Credit);
        ViewBag.ClosingBalance = balance;

        return View();
    }
}

public class LedgerEntry
{
    public DateTime Date { get; set; }
    public string Reference { get; set; } = "";
    public string Type { get; set; } = "";
    public string TypeBadge { get; set; } = "secondary";
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal Balance { get; set; }
    public string? Remarks { get; set; }
    public string? EncryptedId { get; set; }
    public string? Source { get; set; }
}
