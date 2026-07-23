using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Utilities;
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Application.Interfaces.Finance;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Domain.Entities.Accounting;
using PharmaCare.Domain.Entities.Configuration;
using PharmaCare.Domain.Entities.Finance;
using PharmaCare.Domain.Entities.Transactions;
using PharmaCare.Domain.Enums;

namespace PharmaCare.Application.Implementations.Finance;

/// <summary>
/// Service for managing supplier payments with double-entry accounting.
/// </summary>
public class PaymentService : BaseAccountingService, IPaymentService
{
    private readonly IRepository<Payment> _paymentRepository;
    private readonly IRepository<StockMain> _stockMainRepository;
    private readonly IRepository<TransactionType> _transactionTypeRepository;
    private readonly IRepository<VoucherType> _voucherTypeRepository;
    private readonly IRepository<Party> _partyRepository;
    private readonly IRepository<Account> _accountRepository;
    private readonly IFinancialPeriodService _financialPeriodService;

    private const string GRN_TRANSACTION_TYPE_CODE = "GRN";
    private const string PO_TRANSACTION_TYPE_CODE = "PO";
    private const string PURCHASE_RETURN_TRANSACTION_TYPE_CODE = "PRTN";
    private const string PREFIX = "PAY";
    private const string CASH_PAYMENT_VOUCHER_CODE = "CP"; // Cash Payment Voucher
    private const string BANK_PAYMENT_VOUCHER_CODE = "BP"; // Bank Payment Voucher
    private const string CASH_RECEIPT_VOUCHER_CODE = "CR"; // Cash Receipt Voucher (refund in)
    private const string BANK_RECEIPT_VOUCHER_CODE = "BR"; // Bank Receipt Voucher (refund in)
    private const int CashAccountTypeId = AccountingConstants.CashAccountTypeId;
    private const int BankAccountTypeId = AccountingConstants.BankAccountTypeId;
    private static readonly string SupplierPaymentType = PaymentType.PAYMENT.ToString();
    private static readonly string RefundPaymentType = PaymentType.REFUND.ToString();

    private readonly IPurchaseService _purchaseService;

    public PaymentService(
        IRepository<Payment> paymentRepository,
        IRepository<StockMain> stockMainRepository,
        IRepository<TransactionType> transactionTypeRepository,
        IRepository<Voucher> voucherRepository,
        IRepository<VoucherType> voucherTypeRepository,
        IRepository<Party> partyRepository,
        IRepository<Account> accountRepository,
        IUnitOfWork unitOfWork,
        IFinancialPeriodService financialPeriodService,
        IPurchaseService purchaseService)
        : base(unitOfWork, voucherRepository)
    {
        _paymentRepository = paymentRepository;
        _stockMainRepository = stockMainRepository;
        _transactionTypeRepository = transactionTypeRepository;
        _voucherTypeRepository = voucherTypeRepository;
        _partyRepository = partyRepository;
        _accountRepository = accountRepository;
        _financialPeriodService = financialPeriodService;
        _purchaseService = purchaseService;
    }

    public async Task<IEnumerable<Payment>> GetAllSupplierPaymentsAsync()
    {
        return await _paymentRepository.Query()
            .Include(p => p.Party)
            .Include(p => p.StockMain)
            .Include(p => p.Account)
            .Where(p => p.PaymentType == SupplierPaymentType)
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.PaymentID)
            .ToListAsync();
    }

    public async Task<Payment?> GetByIdAsync(int id)
    {
        return await _paymentRepository.Query()
            .Include(p => p.Party)
            .Include(p => p.StockMain)
                .ThenInclude(s => s!.TransactionType)
            .Include(p => p.Account)
            .FirstOrDefaultAsync(p => p.PaymentID == id);
    }

    public async Task<IEnumerable<Payment>> GetPaymentsByTransactionAsync(int stockMainId)
    {
        return await _paymentRepository.Query()
            .Include(p => p.Account)
            .Where(p => p.StockMain_ID == stockMainId)
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StockMain>> GetPendingGrnsAsync(int? supplierId = null, bool includePaid = false)
    {
        var query = _stockMainRepository.Query()
            .AsNoTracking()
            .Include(s => s.TransactionType)
            .Include(s => s.Party)
            .Where(s => s.TransactionType!.Code == GRN_TRANSACTION_TYPE_CODE 
                     && s.Status == "Approved");

        if (supplierId.HasValue)
        {
            query = query.Where(s => s.Party_ID == supplierId.Value);
        }

        var grns = await query
            .OrderByDescending(s => s.TransactionDate)
            .ToListAsync();

        if (grns.Count == 0)
        {
            return grns;
        }

        var grnIds = grns.Select(g => g.StockMainID).ToList();
        var returnTotals = await _stockMainRepository.Query()
            .Include(s => s.TransactionType)
            .Where(s => s.TransactionType!.Code == PURCHASE_RETURN_TRANSACTION_TYPE_CODE
                     && s.Status != "Void"
                     && s.ReferenceStockMain_ID.HasValue
                     && grnIds.Contains(s.ReferenceStockMain_ID.Value))
            .GroupBy(s => s.ReferenceStockMain_ID!.Value)
            .Select(g => new
            {
                GrnId = g.Key,
                TotalReturn = g.Sum(s => s.TotalAmount)
            })
            .ToListAsync();

        var returnLookup = returnTotals.ToDictionary(x => x.GrnId, x => x.TotalReturn);
        foreach (var grn in grns)
        {
            returnLookup.TryGetValue(grn.StockMainID, out var totalReturns);
            grn.BalanceAmount = Math.Max(0, grn.TotalAmount - totalReturns - grn.PaidAmount);
            grn.PaymentStatus = grn.BalanceAmount <= 0
                ? PaymentStatus.Paid.ToString()
                : (grn.PaidAmount <= 0 ? PaymentStatus.Unpaid.ToString() : PaymentStatus.Partial.ToString());
        }

        if (includePaid)
        {
            return grns;
        }

        return grns.Where(g => g.BalanceAmount > 0).ToList();
    }

    public async Task<PharmaCare.Application.DTOs.PagedResult<StockMain>> GetPagedPendingGrnsAsync(
        int? supplierId, DateTime? fromDate, DateTime? toDate, string? status, int page, int pageSize)
    {
        var query = _stockMainRepository.Query()
            .AsNoTracking()
            .Include(s => s.TransactionType)
            .Include(s => s.Party)
            .Where(s => s.TransactionType!.Code == GRN_TRANSACTION_TYPE_CODE 
                     && s.Status == "Approved");

        if (supplierId.HasValue)
            query = query.Where(s => s.Party_ID == supplierId.Value);

        if (fromDate.HasValue)
            query = query.Where(s => s.TransactionDate >= fromDate.Value.Date);

        if (toDate.HasValue)
            query = query.Where(s => s.TransactionDate <= toDate.Value.Date.AddDays(1).AddTicks(-1));

        // To support accurate status filtering, fetch all matched ones before Pagination?
        // Since we need to calculate total returns per GRN. 
        // For accurate server-side pagination with computed properties, 
        // we will fetch the data and do it in-memory if needed, but for performance let's try EF projection.

        var grnsWithReturns = query.Select(g => new
        {
            Grn = g,
            TotalReturns = _stockMainRepository.Query()
                .Where(r => r.TransactionType!.Code == PURCHASE_RETURN_TRANSACTION_TYPE_CODE
                         && r.Status != "Void"
                         && r.ReferenceStockMain_ID == g.StockMainID)
                .Sum(r => (decimal?)r.TotalAmount) ?? 0m
        });

        if (!string.IsNullOrEmpty(status) && status != "All")
        {
            if (status == "Paid")
                grnsWithReturns = grnsWithReturns.Where(x => (x.Grn.TotalAmount - x.TotalReturns - x.Grn.PaidAmount) <= 0);
            else if (status == "Unpaid")
                grnsWithReturns = grnsWithReturns.Where(x => (x.Grn.TotalAmount - x.TotalReturns - x.Grn.PaidAmount) > 0 && x.Grn.PaidAmount <= 0);
            else if (status == "Partial")
                grnsWithReturns = grnsWithReturns.Where(x => (x.Grn.TotalAmount - x.TotalReturns - x.Grn.PaidAmount) > 0 && x.Grn.PaidAmount > 0);
        }

        int totalCount = await grnsWithReturns.CountAsync();

        var paginatedItems = await grnsWithReturns
            .OrderByDescending(x => x.Grn.TransactionDate)
            .ThenByDescending(x => x.Grn.StockMainID)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var resultItems = new List<StockMain>();
        foreach (var item in paginatedItems)
        {
            var grn = item.Grn;
            grn.BalanceAmount = Math.Max(0, grn.TotalAmount - item.TotalReturns - grn.PaidAmount);
            grn.PaymentStatus = grn.BalanceAmount <= 0
                ? PaymentStatus.Paid.ToString()
                : (grn.PaidAmount <= 0 ? PaymentStatus.Unpaid.ToString() : PaymentStatus.Partial.ToString());
            resultItems.Add(grn);
        }

        return new PharmaCare.Application.DTOs.PagedResult<StockMain>
        {
            Items = resultItems,
            TotalCount = totalCount,
            CurrentPage = page,
            PageSize = pageSize
        };
    }

    public async Task<decimal> GetSupplierPayableToMeAsync(int supplierId)
    {
        if (supplierId <= 0)
            return 0;

        var supplier = await _partyRepository.Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PartyID == supplierId && (p.PartyType == "Supplier" || p.PartyType == "Both"));

        if (supplier == null)
            return 0;

        var totalPurchases = await _stockMainRepository.Query()
            .Include(s => s.TransactionType)
            .Where(s => s.Party_ID == supplierId
                        && s.TransactionType!.Code == GRN_TRANSACTION_TYPE_CODE
                        && s.Status != "Void")
            .SumAsync(s => (decimal?)s.TotalAmount) ?? 0;

        var totalPurchaseReturns = await _stockMainRepository.Query()
            .Include(s => s.TransactionType)
            .Where(s => s.Party_ID == supplierId
                        && s.TransactionType!.Code == PURCHASE_RETURN_TRANSACTION_TYPE_CODE
                        && s.Status != "Void")
            .SumAsync(s => (decimal?)s.TotalAmount) ?? 0;

        var totalPayments = await _paymentRepository.Query()
            .Include(p => p.StockMain)
            .Where(p => p.Party_ID == supplierId
                        && p.PaymentType == SupplierPaymentType
                        && (!p.StockMain_ID.HasValue || p.StockMain == null || p.StockMain.Status != "Void"))
            .SumAsync(p => (decimal?)p.Amount) ?? 0;

        // Positive = payable to supplier; Negative = supplier owes company.
        var supplierNetPayable = supplier.OpeningBalance + totalPurchases - totalPurchaseReturns - totalPayments;
        return supplierNetPayable < 0 ? Math.Abs(supplierNetPayable) : 0;
    }

    public async Task<Payment> CreatePaymentAsync(Payment payment, int userId)
    {
        if (await _financialPeriodService.IsPeriodLockedAsync(payment.PaymentDate))
            throw new InvalidOperationException("The financial period for this payment date is closed.");

        return await ExecuteInTransactionAsync(async () =>
        {
            // The StockMain might already be tracked in the same DbContext scope (e.g. by ApproveAsync)
            // so we do NOT use AsNoTracking() here, to avoid key collision when calling Update().
            var stockMain = await _stockMainRepository.Query()
                .Include(s => s.TransactionType)
                .Include(s => s.Party)
                    .ThenInclude(p => p!.Account)
                .Include(s => s.StockDetails)
                .FirstOrDefaultAsync(s => s.StockMainID == payment.StockMain_ID);

            if (stockMain == null)
                throw new InvalidOperationException("Transaction not found.");

            if (string.Equals(payment.PaymentMethod, "Cheque", StringComparison.OrdinalIgnoreCase) && payment.ChequeDate.HasValue)
            {
                if (payment.ChequeDate.Value > DateTime.Now.AddMonths(6))
                    throw new InvalidOperationException("Cheque date cannot be more than 6 months in the future.");
            }

            if (!string.Equals(stockMain.Status, "Approved", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Payments can only be made against approved transactions.");

            var transactionCode = stockMain.TransactionType?.Code;
            if (!string.Equals(transactionCode, GRN_TRANSACTION_TYPE_CODE, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(transactionCode, PO_TRANSACTION_TYPE_CODE, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Payments are only supported for GRN or PO transactions.");
            }

            if (string.Equals(transactionCode, PO_TRANSACTION_TYPE_CODE, StringComparison.OrdinalIgnoreCase))
            {
                stockMain.PaidAmount = await _paymentRepository.Query()
                    .Where(p => p.PaymentType == SupplierPaymentType
                             && p.StockMain_ID == stockMain.StockMainID)
                    .SumAsync(p => (decimal?)p.Amount) ?? 0;
            }

            if (payment.Amount <= 0)
                throw new InvalidOperationException("Payment amount must be greater than zero.");

            var outstandingBeforePayment = await CalculateOutstandingAmountAsync(stockMain, stockMain.PaidAmount);
            if (outstandingBeforePayment <= 0)
                throw new InvalidOperationException("This transaction has no outstanding balance.");

            if (payment.Amount > outstandingBeforePayment)
                throw new InvalidOperationException($"Payment amount ({payment.Amount:N2}) exceeds balance ({outstandingBeforePayment:N2}).");

            // Get the payment account (Cash/Bank)
            var paymentAccount = await _accountRepository.Query()
                .FirstOrDefaultAsync(a => a.AccountID == payment.Account_ID);

            if (paymentAccount == null)
                throw new InvalidOperationException("Payment account not found.");

            // Get supplier's linked account directly (no more name-matching)
            var supplierAccount = stockMain.Party?.Account;
            if (supplierAccount == null)
                throw new InvalidOperationException($"Supplier '{stockMain.Party?.Name}' does not have a linked account. Please update the party record.");

            var partyId = stockMain.Party_ID ?? 0;
            if (partyId <= 0)
                throw new InvalidOperationException("Transaction is not linked to a supplier.");

            // Prevent party tampering from hidden fields by always deriving party from the linked transaction.
            payment.Party_ID = partyId;

            // Generate reference number
            payment.Reference = await GenerateReferenceNoAsync(_paymentRepository, PREFIX);
            payment.PaymentType = SupplierPaymentType; // Supplier payment
            payment.CreatedAt = DateTime.Now;
            payment.CreatedBy = userId;

            // Create accounting voucher
            var voucher = await CreatePaymentVoucherAsync(
                payment,
                supplierAccount,
                paymentAccount,
                stockMain.Party!.Name,
                userId);

            payment.Voucher = voucher;

            // Update transaction balance and re-attach for write
            stockMain.PaidAmount += payment.Amount;
            stockMain.BalanceAmount = await CalculateOutstandingAmountAsync(stockMain, stockMain.PaidAmount);
            stockMain.PaymentStatus = stockMain.BalanceAmount <= 0
                ? PaymentStatus.Paid.ToString()
                : (stockMain.PaidAmount <= 0 ? PaymentStatus.Unpaid.ToString() : PaymentStatus.Partial.ToString());
            stockMain.UpdatedAt = DateTime.Now;
            stockMain.UpdatedBy = userId;

            await _paymentRepository.AddAsync(payment);
            // Explicitly attach the AsNoTracking entity before updating
            _stockMainRepository.Update(stockMain);
            await _unitOfWork.SaveChangesAsync();

            return payment;
        });
    }

    private async Task<decimal> CalculateOutstandingAmountAsync(StockMain stockMain, decimal paidAmount)
    {
        if (stockMain == null)
        {
            return 0;
        }

        var transactionCode = stockMain.TransactionType?.Code;
        if (string.Equals(transactionCode, PO_TRANSACTION_TYPE_CODE, StringComparison.OrdinalIgnoreCase))
        {
            return await CalculatePurchaseOrderOutstandingAmountAsync(stockMain, paidAmount);
        }

        if (!string.Equals(transactionCode, GRN_TRANSACTION_TYPE_CODE, StringComparison.OrdinalIgnoreCase))
        {
            return Math.Max(0, stockMain.TotalAmount - paidAmount);
        }

        var totalReturns = await _stockMainRepository.Query()
            .Include(s => s.TransactionType)
            .Where(s => s.TransactionType!.Code == PURCHASE_RETURN_TRANSACTION_TYPE_CODE
                     && s.ReferenceStockMain_ID == stockMain.StockMainID
                     && s.Status != "Void")
            .SumAsync(s => (decimal?)s.TotalAmount) ?? 0;

        return Math.Max(0, stockMain.TotalAmount - totalReturns - paidAmount);
    }

    private async Task<decimal> CalculatePurchaseOrderOutstandingAmountAsync(StockMain purchaseOrder, decimal paidAmount)
    {
        if (purchaseOrder.StockDetails == null || purchaseOrder.StockDetails.Count == 0)
        {
            // Fallback: re-fetch with StockDetails if not already loaded
            purchaseOrder = await _stockMainRepository.Query()
                .AsNoTracking()
                .Include(s => s.StockDetails)
                .FirstOrDefaultAsync(s => s.StockMainID == purchaseOrder.StockMainID) ?? purchaseOrder;
        }

        var receivedLines = await _stockMainRepository.Query()
            .AsNoTracking()
            .Include(s => s.TransactionType)
            .Where(s => s.TransactionType!.Code == GRN_TRANSACTION_TYPE_CODE
                     && s.Status != "Void"
                     && s.ReferenceStockMain_ID == purchaseOrder.StockMainID)
            .SelectMany(s => s.StockDetails.Select(d => new
            {
                d.Product_ID,
                d.Quantity
            }))
            .ToListAsync();

        var receivedLookup = receivedLines
            .GroupBy(x => x.Product_ID)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

        var remainingTotal = PurchaseOrderMath.RemainingTotal(purchaseOrder, receivedLookup);
        return Math.Max(0, Math.Round(remainingTotal - paidAmount, 2));
    }

    /// <summary>
    /// Creates a payment voucher with double-entry accounting.
    /// Debit: Supplier Account (Accounts Payable) - reduces liability
    /// Credit: Cash/Bank Account - reduces asset
    /// </summary>
    private async Task<Voucher> CreatePaymentVoucherAsync(
        Payment payment, 
        Account supplierAccount, 
        Account cashBankAccount,
        string supplierName,
        int userId)
    {
        // Determine voucher type based on payment method (CP for Cash, BP for Bank/Cheque)
        var isBankLikePayment =
            string.Equals(payment.PaymentMethod, PaymentMethod.Bank.ToString(), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(payment.PaymentMethod, PaymentMethod.Cheque.ToString(), StringComparison.OrdinalIgnoreCase);

        var voucherTypeCode = isBankLikePayment
            ? BANK_PAYMENT_VOUCHER_CODE 
            : CASH_PAYMENT_VOUCHER_CODE;

        var voucherType = await _voucherTypeRepository.Query()
            .FirstOrDefaultAsync(vt => vt.Code == voucherTypeCode);

        if (voucherType == null)
            throw new InvalidOperationException($"Voucher type '{voucherTypeCode}' not found. Please ensure it exists in the database.");

        var voucherNo = await GenerateVoucherNoAsync(voucherTypeCode);

        var voucher = new Voucher
        {
            VoucherType_ID = voucherType.VoucherTypeID,
            VoucherNo = voucherNo,
            VoucherDate = payment.PaymentDate,
            TotalDebit = payment.Amount,
            TotalCredit = payment.Amount,
            Status = "Posted",
            SourceTable = "StockMain",
            SourceID = payment.StockMain_ID, // Link to the purchase transaction
            Narration = $"Payment to supplier: {supplierName}. Ref: {payment.Reference}",
            CreatedAt = DateTime.Now,
            CreatedBy = userId,
            VoucherDetails = new List<VoucherDetail>
            {
                // Debit: Supplier Account (Accounts Payable) - reduces liability
                new VoucherDetail
                {
                    Account_ID = supplierAccount.AccountID,
                    DebitAmount = payment.Amount,
                    CreditAmount = 0,
                    Description = $"Payment to {supplierName}",
                    Party_ID = payment.Party_ID
                },
                // Credit: Cash/Bank Account - reduces asset
                new VoucherDetail
                {
                    Account_ID = cashBankAccount.AccountID,
                    DebitAmount = 0,
                    CreditAmount = payment.Amount,
                    Description = $"Payment via {payment.PaymentMethod}"
                }
            }
        };

        await _voucherRepository.AddAsync(voucher);

        return voucher;
    }

    /// <summary>
    /// Refunds a supplier advance (money the supplier returns to us).
    /// DR: Cash/Bank Account — money received back
    /// CR: Supplier Account — reduces the on-account advance
    /// Bounded by the supplier's available advance; posts a REFUND payment + receipt voucher.
    /// </summary>
    public async Task<Payment> CreateSupplierRefundAsync(Payment payment, int userId)
    {
        if (await _financialPeriodService.IsPeriodLockedAsync(payment.PaymentDate))
            throw new InvalidOperationException("The financial period for this refund date is closed.");

        return await ExecuteInTransactionAsync(async () =>
        {
            if (payment.Amount <= 0)
                throw new InvalidOperationException("Refund amount must be greater than zero.");

            var partyId = payment.Party_ID;
            if (partyId <= 0)
                throw new InvalidOperationException("Supplier is required for a refund.");

            var supplier = await _partyRepository.Query()
                .Include(p => p.Account)
                .FirstOrDefaultAsync(p => p.PartyID == partyId);

            if (supplier == null)
                throw new InvalidOperationException("Supplier not found.");
            if (supplier.Account_ID == null || supplier.Account == null)
                throw new InvalidOperationException($"Supplier '{supplier.Name}' does not have a linked account.");

            // Bound the refund by the supplier's available on-account advance.
            var advance = await _purchaseService.GetSupplierAdvanceAsync(partyId);
            if (advance <= 0)
                throw new InvalidOperationException($"Supplier '{supplier.Name}' has no available advance to refund.");
            if (payment.Amount > advance)
                throw new InvalidOperationException(
                    $"Refund ({payment.Amount:N2}) exceeds the supplier's available advance ({advance:N2}).");

            var refundAccount = await _accountRepository.Query()
                .FirstOrDefaultAsync(a => a.AccountID == payment.Account_ID);
            if (refundAccount == null)
                throw new InvalidOperationException("Refund account not found.");
            if (refundAccount.AccountType_ID != CashAccountTypeId && refundAccount.AccountType_ID != BankAccountTypeId)
                throw new InvalidOperationException("Refund account must be a Cash or Bank account.");

            var isBank = refundAccount.AccountType_ID == BankAccountTypeId;

            payment.Party_ID = partyId;
            payment.StockMain_ID = null;
            payment.Reference = await GenerateReferenceNoAsync(_paymentRepository, PREFIX);
            payment.PaymentType = RefundPaymentType;
            payment.PaymentMethod = isBank ? PaymentMethod.Bank.ToString() : PaymentMethod.Cash.ToString();
            payment.IsVoided = false;
            payment.Remarks = string.IsNullOrWhiteSpace(payment.Remarks)
                ? $"Advance refund from {supplier.Name}"
                : payment.Remarks;
            payment.CreatedAt = DateTime.Now;
            payment.CreatedBy = userId;

            var voucherCode = isBank ? BANK_RECEIPT_VOUCHER_CODE : CASH_RECEIPT_VOUCHER_CODE;
            var voucherType = await _voucherTypeRepository.Query()
                .FirstOrDefaultAsync(vt => vt.Code == voucherCode);
            if (voucherType == null)
                throw new InvalidOperationException($"Voucher type '{voucherCode}' not found.");

            var voucher = new Voucher
            {
                VoucherType_ID = voucherType.VoucherTypeID,
                VoucherNo = await GenerateVoucherNoAsync(voucherCode),
                VoucherDate = payment.PaymentDate,
                TotalDebit = payment.Amount,
                TotalCredit = payment.Amount,
                Status = "Posted",
                SourceTable = "Payment",
                Narration = $"Supplier advance refund from {supplier.Name}. Ref: {payment.Reference}",
                CreatedAt = DateTime.Now,
                CreatedBy = userId,
                VoucherDetails = new List<VoucherDetail>
                {
                    // Debit: Cash/Bank Account — money received back
                    new VoucherDetail
                    {
                        Account_ID = refundAccount.AccountID,
                        DebitAmount = payment.Amount,
                        CreditAmount = 0,
                        Description = $"Advance refund via {payment.PaymentMethod}"
                    },
                    // Credit: Supplier Account — reduces the advance
                    new VoucherDetail
                    {
                        Account_ID = supplier.Account.AccountID,
                        DebitAmount = 0,
                        CreditAmount = payment.Amount,
                        Description = $"Advance refund from {supplier.Name}",
                        Party_ID = partyId
                    }
                }
            };

            await _voucherRepository.AddAsync(voucher);
            payment.Voucher = voucher;
            await _paymentRepository.AddAsync(payment);
            await _unitOfWork.SaveChangesAsync();

            return payment;
        });
    }

    /// <summary>
    /// Gets all payments for a specific party/supplier.
    /// </summary>
    public async Task<IEnumerable<Payment>> GetPaymentsByPartyAsync(int partyId)
    {
        return await _paymentRepository.Query()
            .Include(p => p.Party)
            .Include(p => p.Account)
            .Include(p => p.StockMain)
            .Where(p => p.PaymentType == SupplierPaymentType && p.Party_ID == partyId)
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.PaymentID)
            .ToListAsync();
    }

    /// <summary>
    /// Voids a supplier payment — reverses accounting and recalculates linked transaction balance.
    /// </summary>
    public async Task<bool> VoidPaymentAsync(int paymentId, string reason, int userId)
    {
        return await ExecuteInTransactionAsync(async () =>
        {
            var payment = await _paymentRepository.Query()
                .Include(p => p.Voucher)
                    .ThenInclude(v => v!.VoucherDetails)
                .Include(p => p.StockMain)
                .FirstOrDefaultAsync(p => p.PaymentID == paymentId);

            if (payment == null)
                throw new InvalidOperationException("Payment not found.");

            if (payment.IsVoided)
                throw new InvalidOperationException("This payment has already been voided.");

            if (await _financialPeriodService.IsPeriodLockedAsync(payment.PaymentDate))
                throw new InvalidOperationException("The financial period for this payment date is closed.");

            // Mark payment as voided
            payment.IsVoided = true;
            payment.VoidReason = reason;
            payment.VoidedBy = userId;
            payment.VoidedAt = DateTime.Now;
            _paymentRepository.Update(payment);

            // Create reversal voucher if original voucher exists
            if (payment.Voucher_ID.HasValue)
            {
                await CreateVoucherReversalAsync(payment.Voucher_ID.Value, userId, reason);
            }

            // Recalculate the linked StockMain's payment balance
            if (payment.StockMain_ID.HasValue && payment.StockMain != null)
            {
                var stockMain = payment.StockMain;
                var totalPaid = await _paymentRepository.Query()
                    .Where(p => p.StockMain_ID == stockMain.StockMainID
                             && !p.IsVoided
                             && p.PaymentID != paymentId)
                    .SumAsync(p => p.Amount);

                stockMain.PaidAmount = Math.Max(0, Math.Round(totalPaid, 2));
                stockMain.BalanceAmount = Math.Max(0, Math.Round(stockMain.TotalAmount - stockMain.PaidAmount, 2));
                stockMain.PaymentStatus = stockMain.BalanceAmount <= 0
                    ? PaymentStatus.Paid.ToString()
                    : (stockMain.PaidAmount <= 0 ? PaymentStatus.Unpaid.ToString() : PaymentStatus.Partial.ToString());

                _stockMainRepository.Update(stockMain);
            }

            await _unitOfWork.SaveChangesAsync();
            return true;
        });
    }



}

