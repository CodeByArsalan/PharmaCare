using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.DTOs.Finance;
using PharmaCare.Application.Utilities;
using PharmaCare.Application.Interfaces.Finance;
using PharmaCare.Application.Interfaces.AccountManagement;
using PharmaCare.Domain.Models.Finance;
using PharmaCare.Domain.Models.Inventory;
using PharmaCare.Domain.Models.AccountManagement;
using PharmaCare.Infrastructure.Interfaces;
using PharmaCare.Infrastructure.Interfaces.Accounting;
using PharmaCare.Infrastructure;

namespace PharmaCare.Application.Implementations.Finance;

public class SupplierPaymentService : ISupplierPaymentService
{
    private readonly IRepository<SupplierPayment> _paymentRepo;
    private readonly IRepository<StockMain> _stockMainRepo;
    private readonly IAccountingService _accountingService;
    private readonly IVoucherService _voucherService;
    private readonly PharmaCareDBContext _context;

    // Account Type IDs
    private const int CASH_ACCOUNT_TYPE = 1;
    private const int BANK_ACCOUNT_TYPE = 2;
    private const int SUPPLIER_ACCOUNT_TYPE = 4;
    private const int PURCHASE_INVOICE_TYPE = 2; // InvoiceType for purchases

    // Voucher Type IDs
    private const int BANK_PAYMENT_VOUCHER = 2;
    private const int CASH_PAYMENT_VOUCHER = 4;

    public SupplierPaymentService(
        IRepository<SupplierPayment> paymentRepo,
        IRepository<StockMain> stockMainRepo,
        IAccountingService accountingService,
        IVoucherService voucherService,
        PharmaCareDBContext context)
    {
        _paymentRepo = paymentRepo;
        _stockMainRepo = stockMainRepo;
        _accountingService = accountingService;
        _voucherService = voucherService;
        _context = context;
    }

    #region CRUD Operations

    public async Task<List<SupplierPayment>> GetAllPayments(int? supplierId = null, string? status = null)
    {
        var query = _paymentRepo.FindByCondition(p => p.IsActive);

        if (supplierId.HasValue)
            query = query.Where(p => p.Party_ID == supplierId.Value);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(p => p.Status == status);

        return await query
            .Include(p => p.Party)
            .Include(p => p.StockMain)
            .Include(p => p.Store)
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync();
    }

    public async Task<SupplierPayment?> GetPaymentById(int id)
    {
        return await _paymentRepo.FindByCondition(p => p.SupplierPaymentID == id)
            .Include(p => p.Party)
            .Include(p => p.StockMain)
            .Include(p => p.Store)
            .Include(p => p.AccountVoucher)
            .FirstOrDefaultAsync();
    }

    public async Task<List<SupplierPayment>> GetPaymentsByGrn(int stockMainId)
    {
        return await _paymentRepo.FindByCondition(p => p.StockMain_ID == stockMainId && p.IsActive)
            .Include(p => p.Party)
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync();
    }

    public Task<string> GeneratePaymentNumber()
    {
        return Task.FromResult(UniqueIdGenerator.Generate("SP"));
    }

    #endregion

    #region Outstanding Purchases

    public async Task<List<GrnOutstandingDto>> GetOutstandingGrns(int? supplierId = null)
    {
        // Query StockMain records with InvoiceType=2 (PURCHASE) that have outstanding balance
        var query = _stockMainRepo.FindByCondition(sm => 
            sm.InvoiceType_ID == PURCHASE_INVOICE_TYPE && 
            sm.PaymentStatus != "Paid");

        if (supplierId.HasValue)
            query = query.Where(sm => sm.Party_ID == supplierId.Value);

        var purchases = await query
            .Include(sm => sm.Party)
            .Include(sm => sm.StockDetails)
            .OrderByDescending(sm => sm.CreatedDate)
            .ToListAsync();

        return purchases.Select(sm => new GrnOutstandingDto
        {
            StockMainID = sm.StockMainID,
            GrnNumber = sm.InvoiceNo,
            CreatedDate = sm.CreatedDate,
            SupplierId = sm.Party_ID ?? 0,
            SupplierName = sm.Party?.PartyName ?? "Unknown",
            TotalAmount = sm.TotalAmount > 0 ? sm.TotalAmount : sm.StockDetails.Sum(d => d.Quantity * d.UnitPrice),
            AmountPaid = sm.PaidAmount,
            ReturnedAmount = sm.ReturnedAmount,
            BalanceAmount = sm.TotalAmount - sm.PaidAmount - sm.ReturnedAmount,
            PaymentStatus = sm.PaymentStatus ?? "Unpaid"
        }).ToList();
    }

    public async Task<decimal> GetTotalOutstandingForSupplier(int supplierId)
    {
        var purchases = await _stockMainRepo.FindByCondition(sm => 
            sm.Party_ID == supplierId && 
            sm.InvoiceType_ID == PURCHASE_INVOICE_TYPE && 
            sm.PaymentStatus != "Paid")
            .Include(sm => sm.StockDetails)
            .ToListAsync();

        return purchases.Sum(sm => sm.TotalAmount - sm.PaidAmount - sm.ReturnedAmount);
    }

    #endregion

    #region Payment Processing

    public async Task<bool> CreatePayment(SupplierPayment payment, int loginUserId)
    {
        // 1. Validate purchase exists and has balance
        var purchase = await _stockMainRepo.FindByCondition(sm => sm.StockMainID == payment.StockMain_ID)
            .Include(sm => sm.StockDetails)
            .FirstOrDefaultAsync();

        if (purchase == null)
            throw new InvalidOperationException("Purchase not found");

        // Calculate total if not set
        if (purchase.TotalAmount == 0)
        {
            purchase.TotalAmount = purchase.StockDetails.Sum(d => d.Quantity * d.UnitPrice);
            purchase.BalanceAmount = purchase.TotalAmount - purchase.PaidAmount - purchase.ReturnedAmount;
        }

        var currentBalance = purchase.TotalAmount - purchase.PaidAmount - purchase.ReturnedAmount;

        if (currentBalance <= 0)
            throw new InvalidOperationException("This purchase is already fully paid");

        if (payment.AmountPaid <= 0)
            throw new InvalidOperationException("Payment amount must be greater than zero");

        if (payment.AmountPaid > currentBalance)
            throw new InvalidOperationException($"Payment amount ({payment.AmountPaid:C}) exceeds balance ({currentBalance:C})");

        // 2. Generate payment number
        payment.PaymentNumber = await GeneratePaymentNumber();
        payment.GrnAmount = purchase.TotalAmount;
        payment.BalanceAfter = currentBalance - payment.AmountPaid;
        payment.PaymentType = payment.AmountPaid >= currentBalance ? "Full" : "Partial";
        payment.Status = "Paid";
        payment.CreatedBy = loginUserId;
        payment.CreatedDate = DateTime.Now;
        payment.IsActive = true;

        // 3. Create Voucher (Replaces JournalEntry)
        var paymentAccountTypeId = payment.PaymentMethod == "Bank" ? BANK_ACCOUNT_TYPE : CASH_ACCOUNT_TYPE;
        var voucherTypeId = payment.PaymentMethod == "Bank" ? BANK_PAYMENT_VOUCHER : CASH_PAYMENT_VOUCHER;

        var supplierAccount = await _accountingService.GetFirstAccountByTypeId(SUPPLIER_ACCOUNT_TYPE);
        var paymentAccount = await _accountingService.GetFirstAccountByTypeId(paymentAccountTypeId);

        if (supplierAccount == null)
            throw new InvalidOperationException("Supplier account not found in Chart of Accounts");

        if (paymentAccount == null)
            throw new InvalidOperationException($"{payment.PaymentMethod} account not found in Chart of Accounts");

        var voucherRequest = new CreateVoucherRequest
        {
            VoucherTypeId = voucherTypeId,
            VoucherDate = payment.PaymentDate,
            SourceTable = "SupplierPayments",
            SourceId = 0, // Will update after insert
            StoreId = payment.Store_ID,
            Narration = $"Payment to supplier - {purchase.InvoiceNo} ({payment.PaymentNumber})",
            CreatedBy = loginUserId,
            Lines = new List<CreateVoucherLineRequest>
            {
                // Debit Supplier (Reduce Liability)
                new CreateVoucherLineRequest
                {
                    AccountId = supplierAccount.AccountID,
                    Dr = payment.AmountPaid,
                    Cr = 0,
                    Particulars = $"Payment against {purchase.InvoiceNo}",
                    StoreId = payment.Store_ID
                },
                // Credit Cash/Bank (Reduce Asset)
                new CreateVoucherLineRequest
                {
                    AccountId = paymentAccount.AccountID,
                    Dr = 0,
                    Cr = payment.AmountPaid,
                    Particulars = $"Payment via {payment.PaymentMethod}",
                    StoreId = payment.Store_ID
                }
            }
        };

        var voucher = await _voucherService.CreateVoucherAsync(voucherRequest);
        payment.Voucher_ID = voucher.VoucherID;

        // 4. Insert Payment
        var result = await _paymentRepo.Insert(payment);

        if (result)
        {
            // Set SourceID of voucher to the newly created payment ID
            // Note: We'd need to update the voucher directly or via service if exposed. 
            // For now, let's assume CreateVoucherAsync returns the tracked entity 
            // but we might need to explicit update if not automatically tracked in same context safely.
            // Since _voucherService uses its own repo, let's skip back-updating SourceID for now or handle it if critical.
            // Actually, we can update the payment entity's ID into the voucher if we want bi-directional link.
            // But usually the payment->voucher FK is enough.
            
            await _context.SaveChangesAsync();

            // 5. Update StockMain payment tracking
            purchase.PaidAmount += payment.AmountPaid;
            purchase.BalanceAmount = purchase.TotalAmount - purchase.PaidAmount - purchase.ReturnedAmount;
            purchase.PaymentStatus = purchase.BalanceAmount <= 0 ? "Paid" : "Partial";

            await _stockMainRepo.Update(purchase);
        }

        return result;
    }

    public async Task<bool> CancelPayment(int paymentId, int userId)
    {
        var payment = await _paymentRepo.FindByCondition(p => p.SupplierPaymentID == paymentId)
            .FirstOrDefaultAsync();

        if (payment == null)
            throw new InvalidOperationException("Payment not found");

        if (payment.Status == "Cancelled")
            throw new InvalidOperationException("Payment is already cancelled");

        // Reverse the voucher
        if (payment.Voucher_ID.HasValue)
        {
            await _voucherService.ReverseVoucherAsync(payment.Voucher_ID.Value, "Payment Cancelled", userId);
        }


        payment.Status = "Cancelled";
        payment.UpdatedBy = userId;
        payment.UpdatedDate = DateTime.Now;

        var result = await _paymentRepo.Update(payment);

        if (result)
        {
            // Reverse StockMain payment tracking
            var purchase = await _stockMainRepo.FindByCondition(sm => sm.StockMainID == payment.StockMain_ID)
                .FirstOrDefaultAsync();

            if (purchase != null)
            {
                purchase.PaidAmount -= payment.AmountPaid;
                purchase.BalanceAmount = purchase.TotalAmount - purchase.PaidAmount - purchase.ReturnedAmount;
                purchase.PaymentStatus = purchase.PaidAmount <= 0 ? "Unpaid" : (purchase.BalanceAmount <= 0 ? "Paid" : "Partial");

                await _stockMainRepo.Update(purchase);
            }
        }

        return result;
    }

    #endregion

    #region Reports

    public async Task<SupplierPaymentSummaryDto> GetPaymentSummary(int? storeId = null)
    {
        var today = DateTime.Today;
        var firstOfMonth = new DateTime(today.Year, today.Month, 1);

        var paymentsQuery = _paymentRepo.FindByCondition(p => p.IsActive && p.Status == "Paid");
        var purchasesQuery = _stockMainRepo.FindByCondition(sm => sm.InvoiceType_ID == PURCHASE_INVOICE_TYPE);

        if (storeId.HasValue)
        {
            paymentsQuery = paymentsQuery.Where(p => p.Store_ID == storeId.Value);
            purchasesQuery = purchasesQuery.Where(sm => sm.Store_ID == storeId.Value);
        }

        var todayPayments = await paymentsQuery
            .Where(p => p.PaymentDate.Date == today)
            .SumAsync(p => p.AmountPaid);

        var monthPayments = await paymentsQuery
            .Where(p => p.PaymentDate >= firstOfMonth)
            .SumAsync(p => p.AmountPaid);

        var unpaidPurchases = await purchasesQuery
            .Where(sm => sm.PaymentStatus == "Unpaid" || sm.PaymentStatus == null)
            .CountAsync();

        var partialPurchases = await purchasesQuery
            .Where(sm => sm.PaymentStatus == "Partial")
            .CountAsync();

        var outstandingPurchases = await purchasesQuery
            .Where(sm => sm.PaymentStatus != "Paid")
            .Include(sm => sm.StockDetails)
            .Include(sm => sm.Party)
            .ToListAsync();

        var totalOutstanding = outstandingPurchases.Sum(sm =>
            sm.TotalAmount - sm.PaidAmount - sm.ReturnedAmount);

        var topSuppliers = outstandingPurchases
            .Where(sm => sm.Party_ID.HasValue)
            .GroupBy(sm => new { sm.Party_ID, SupplierName = sm.Party?.PartyName ?? "Unknown" })
            .Select(g => new SupplierOutstandingDto
            {
                PartyID = g.Key.Party_ID ?? 0,
                SupplierName = g.Key.SupplierName,
                TotalOutstanding = g.Sum(x => x.BalanceAmount > 0 ? x.BalanceAmount : x.TotalAmount - x.PaidAmount),
                UnpaidGrnCount = g.Count()
            })
            .OrderByDescending(s => s.TotalOutstanding)
            .Take(5)
            .ToList();

        return new SupplierPaymentSummaryDto
        {
            TotalOutstanding = totalOutstanding,
            TotalPaidToday = todayPayments,
            TotalPaidThisMonth = monthPayments,
            UnpaidGrnCount = unpaidPurchases,
            PartiallyPaidGrnCount = partialPurchases,
            TopSuppliersByOutstanding = topSuppliers
        };
    }

    #endregion
}
