using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Finance;
using PharmaCare.Domain.Entities.Accounting;
using PharmaCare.Domain.Entities.Configuration;
using PharmaCare.Domain.Entities.Finance;
using PharmaCare.Domain.Entities.Transactions;
using PharmaCare.Domain.Enums;

namespace PharmaCare.Application.Implementations.Finance;

/// <summary>
/// Service for managing supplier payments with double-entry accounting.
/// </summary>
public class PaymentService : IPaymentService
{
    private readonly IRepository<Payment> _paymentRepository;
    private readonly IRepository<StockMain> _stockMainRepository;
    private readonly IRepository<TransactionType> _transactionTypeRepository;
    private readonly IRepository<Voucher> _voucherRepository;
    private readonly IRepository<VoucherType> _voucherTypeRepository;
    private readonly IRepository<Party> _partyRepository;
    private readonly IRepository<Account> _accountRepository;
    private readonly IUnitOfWork _unitOfWork;

    private const string GRN_TRANSACTION_TYPE_CODE = "GRN";
    private const string PO_TRANSACTION_TYPE_CODE = "PO";
    private const string PURCHASE_RETURN_TRANSACTION_TYPE_CODE = "PRTN";
    private const string PREFIX = "PAY";
    private const string CASH_PAYMENT_VOUCHER_CODE = "CP"; // Cash Payment Voucher
    private const string BANK_PAYMENT_VOUCHER_CODE = "BP"; // Bank Payment Voucher
    private static readonly string SupplierPaymentType = PaymentType.PAYMENT.ToString();

    public PaymentService(
        IRepository<Payment> paymentRepository,
        IRepository<StockMain> stockMainRepository,
        IRepository<TransactionType> transactionTypeRepository,
        IRepository<Voucher> voucherRepository,
        IRepository<VoucherType> voucherTypeRepository,
        IRepository<Party> partyRepository,
        IRepository<Account> accountRepository,
        IUnitOfWork unitOfWork)
    {
        _paymentRepository = paymentRepository;
        _stockMainRepository = stockMainRepository;
        _transactionTypeRepository = transactionTypeRepository;
        _voucherRepository = voucherRepository;
        _voucherTypeRepository = voucherTypeRepository;
        _partyRepository = partyRepository;
        _accountRepository = accountRepository;
        _unitOfWork = unitOfWork;
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

    public async Task<IEnumerable<StockMain>> GetPendingGrnsAsync(int? supplierId = null)
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

        return grns.Where(g => g.BalanceAmount > 0).ToList();
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
        return await ExecuteInTransactionAsync(async () =>
        {
            // Validate the transaction exists (include type + party + linked account)
            var stockMain = await _stockMainRepository.Query()
                .Include(s => s.TransactionType)
                .Include(s => s.Party)
                    .ThenInclude(p => p!.Account)
                .FirstOrDefaultAsync(s => s.StockMainID == payment.StockMain_ID);

            if (stockMain == null)
                throw new InvalidOperationException("Transaction not found.");

            if (!string.Equals(stockMain.Status, "Approved", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Payments can only be made against approved transactions.");

            var transactionCode = stockMain.TransactionType?.Code;
            if (!string.Equals(transactionCode, GRN_TRANSACTION_TYPE_CODE, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(transactionCode, PO_TRANSACTION_TYPE_CODE, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Payments are only supported for GRN or PO transactions.");
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
            payment.Reference = await GenerateReferenceNoAsync();
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

            // Update transaction balance
            stockMain.PaidAmount += payment.Amount;
            stockMain.BalanceAmount = await CalculateOutstandingAmountAsync(stockMain, stockMain.PaidAmount);
            stockMain.PaymentStatus = stockMain.BalanceAmount <= 0
                ? PaymentStatus.Paid.ToString()
                : (stockMain.PaidAmount <= 0 ? PaymentStatus.Unpaid.ToString() : PaymentStatus.Partial.ToString());
            stockMain.UpdatedAt = DateTime.Now;
            stockMain.UpdatedBy = userId;

            await _paymentRepository.AddAsync(payment);
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

    private async Task<string> GenerateReferenceNoAsync()
    {
        var datePrefix = $"{PREFIX}-{DateTime.Now:yyyyMMdd}-";

        var lastPayment = await _paymentRepository.Query()
            .Where(p => p.Reference != null && p.Reference.StartsWith(datePrefix))
            .OrderByDescending(p => p.Reference)
            .FirstOrDefaultAsync();

        int nextNum = 1;
        if (lastPayment != null && lastPayment.Reference != null)
        {
            var parts = lastPayment.Reference.Split('-');
            if (parts.Length > 2 && int.TryParse(parts.Last(), out int lastNum))
            {
                nextNum = lastNum + 1;
            }
        }

        return $"{datePrefix}{nextNum:D4}";
    }

    private async Task<string> GenerateVoucherNoAsync(string voucherTypeCode)
    {
        var datePrefix = $"{voucherTypeCode}-{DateTime.Now:yyyyMMdd}-";

        var lastVoucher = await _voucherRepository.Query()
            .Where(v => v.VoucherNo.StartsWith(datePrefix))
            .OrderByDescending(v => v.VoucherNo)
            .FirstOrDefaultAsync();

        int nextNum = 1;
        if (lastVoucher != null)
        {
            var parts = lastVoucher.VoucherNo.Split('-');
            if (parts.Length > 2 && int.TryParse(parts.Last(), out int lastNum))
            {
                nextNum = lastNum + 1;
            }
        }

        return $"{datePrefix}{nextNum:D4}";
    }

    /// <summary>
    /// Creates an advance payment to a supplier (not linked to any GRN).
    /// DR: Supplier Account (creates debit balance / reduces payable)
    /// CR: Cash/Bank Account
    /// The debit balance will automatically offset against future purchases.
    /// </summary>
    public async Task<Payment> CreateAdvancePaymentAsync(Payment payment, int userId)
    {
        return await ExecuteInTransactionAsync(async () =>
        {
            if (payment.Amount <= 0)
                throw new InvalidOperationException("Payment amount must be greater than zero.");

            // Get the supplier with their linked account
            var supplier = await _partyRepository.Query()
                .Include(p => p.Account)
                .FirstOrDefaultAsync(p => p.PartyID == payment.Party_ID);

            if (supplier == null)
                throw new InvalidOperationException("Supplier not found.");

            if (supplier.Account_ID == null)
                throw new InvalidOperationException($"Supplier '{supplier.Name}' does not have a linked account. Please update the party record.");

            var supplierAccount = supplier.Account!;

            // Get the payment account (Cash/Bank)
            var paymentAccount = await _accountRepository.Query()
                .FirstOrDefaultAsync(a => a.AccountID == payment.Account_ID);

            if (paymentAccount == null)
                throw new InvalidOperationException("Payment account not found.");

            // Generate reference number
            payment.Reference = await GenerateReferenceNoAsync();
            payment.PaymentType = SupplierPaymentType; // Supplier payment
            payment.StockMain_ID = null; // No GRN link - this is an advance
            payment.Remarks = string.IsNullOrWhiteSpace(payment.Remarks)
                ? $"Advance payment to {supplier.Name}"
                : payment.Remarks;
            payment.CreatedAt = DateTime.Now;
            payment.CreatedBy = userId;

            // Create accounting voucher (same as regular payment: DR Supplier, CR Cash/Bank)
            var voucher = await CreatePaymentVoucherAsync(
                payment,
                supplierAccount,
                paymentAccount,
                supplier.Name,
                userId);

            payment.Voucher = voucher;

            await _paymentRepository.AddAsync(payment);
            await _unitOfWork.SaveChangesAsync();

            return payment;
        });
    }

    /// <summary>
    /// Gets all advance payments (payments without a linked transaction).
    /// </summary>
    public async Task<IEnumerable<Payment>> GetAdvancePaymentsAsync()
    {
        return await _paymentRepository.Query()
            .Include(p => p.Party)
            .Include(p => p.Account)
            .Where(p => p.PaymentType == SupplierPaymentType && p.StockMain_ID == null)
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.PaymentID)
            .ToListAsync();
    }

    private async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var result = await operation();
            await _unitOfWork.CommitTransactionAsync();
            return result;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }
}

