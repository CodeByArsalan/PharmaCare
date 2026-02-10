using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Finance;
using PharmaCare.Application.Settings;
using PharmaCare.Domain.Entities.Accounting;
using PharmaCare.Domain.Entities.Configuration;
using PharmaCare.Domain.Entities.Finance;
using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.Application.Implementations.Finance;

/// <summary>
/// Service for managing customer receipts with double-entry accounting.
/// </summary>
public class CustomerPaymentService : ICustomerPaymentService
{
    private readonly IRepository<Payment> _paymentRepository;
    private readonly IRepository<StockMain> _stockMainRepository;
    private readonly IRepository<TransactionType> _transactionTypeRepository;
    private readonly IRepository<Voucher> _voucherRepository;
    private readonly IRepository<VoucherType> _voucherTypeRepository;
    private readonly IRepository<Party> _partyRepository;
    private readonly IRepository<Account> _accountRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly SystemAccountSettings _systemAccountSettings;

    private const string SALE_TRANSACTION_TYPE_CODE = "SALE";
    private const string PREFIX = "REC";
    private const string VOUCHER_TYPE_CODE = "RV"; // Receipt Voucher

    public CustomerPaymentService(
        IRepository<Payment> paymentRepository,
        IRepository<StockMain> stockMainRepository,
        IRepository<TransactionType> transactionTypeRepository,
        IRepository<Voucher> voucherRepository,
        IRepository<VoucherType> voucherTypeRepository,
        IRepository<Party> partyRepository,
        IRepository<Account> accountRepository,
        IUnitOfWork unitOfWork,
        IOptions<SystemAccountSettings> systemAccountSettings)
    {
        _paymentRepository = paymentRepository;
        _stockMainRepository = stockMainRepository;
        _transactionTypeRepository = transactionTypeRepository;
        _voucherRepository = voucherRepository;
        _voucherTypeRepository = voucherTypeRepository;
        _partyRepository = partyRepository;
        _accountRepository = accountRepository;
        _unitOfWork = unitOfWork;
        _systemAccountSettings = systemAccountSettings.Value;
    }

    public async Task<IEnumerable<Payment>> GetAllCustomerReceiptsAsync()
    {
        return await _paymentRepository.Query()
            .Include(p => p.Party)
            .Include(p => p.StockMain)
            .Include(p => p.Account)
            .Where(p => p.PaymentType == "RECEIPT")
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.PaymentID)
            .ToListAsync();
    }

    public async Task<Payment?> GetByIdAsync(int id)
    {
        return await _paymentRepository.Query()
            .Include(p => p.Party)
            .Include(p => p.StockMain)
            .Include(p => p.Account)
            .FirstOrDefaultAsync(p => p.PaymentID == id);
    }

    public async Task<IEnumerable<Payment>> GetReceiptsByTransactionAsync(int stockMainId)
    {
        return await _paymentRepository.Query()
            .Include(p => p.Account)
            .Where(p => p.StockMain_ID == stockMainId)
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StockMain>> GetPendingSalesAsync(int? customerId = null)
    {
        var query = _stockMainRepository.Query()
            .Include(s => s.TransactionType)
            .Include(s => s.Party)
            .Where(s => s.TransactionType!.Code == SALE_TRANSACTION_TYPE_CODE 
                     && s.Status == "Approved" 
                     && s.BalanceAmount > 0);

        if (customerId.HasValue)
        {
            query = query.Where(s => s.Party_ID == customerId.Value);
        }

        return await query
            .OrderByDescending(s => s.TransactionDate)
            .ToListAsync();
    }

    public async Task<Payment> CreateReceiptAsync(Payment payment, int userId)
    {
        // Validate the transaction exists (include Party and their Account)
        var stockMain = await _stockMainRepository.Query()
            .Include(s => s.Party)
                .ThenInclude(p => p!.Account)
            .FirstOrDefaultAsync(s => s.StockMainID == payment.StockMain_ID);

        if (stockMain == null)
            throw new InvalidOperationException("Transaction not found.");

        if (payment.Amount <= 0)
            throw new InvalidOperationException("Receipt amount must be greater than zero.");

        if (payment.Amount > stockMain.BalanceAmount)
            throw new InvalidOperationException($"Receipt amount ({payment.Amount:N2}) exceeds balance ({stockMain.BalanceAmount:N2}).");

        // Get the receipt account (Cash/Bank)
        var receiptAccount = await _accountRepository.Query()
            .FirstOrDefaultAsync(a => a.AccountID == payment.Account_ID);

        if (receiptAccount == null)
            throw new InvalidOperationException("Receipt account not found.");

        // Get customer's linked account directly
        var customerAccount = stockMain.Party?.Account;
        if (customerAccount == null)
            throw new InvalidOperationException($"Customer '{stockMain.Party?.Name}' does not have a linked account. Please update the party record.");

        // Generate reference number
        payment.Reference = await GenerateReferenceNoAsync();
        payment.PaymentType = "RECEIPT"; // Customer receipt
        payment.CreatedAt = DateTime.Now;
        payment.CreatedBy = userId;

        // Create accounting voucher
        var voucher = await CreateReceiptVoucherAsync(
            payment, 
            customerAccount, 
            receiptAccount, 
            stockMain.Party!.Name, 
            userId);

        payment.Voucher_ID = voucher.VoucherID;

        // Update transaction balance
        stockMain.PaidAmount += payment.Amount;
        stockMain.BalanceAmount = stockMain.TotalAmount - stockMain.PaidAmount;
        stockMain.PaymentStatus = stockMain.BalanceAmount <= 0 ? "Paid" : "Partial";
        stockMain.UpdatedAt = DateTime.Now;
        stockMain.UpdatedBy = userId;

        await _paymentRepository.AddAsync(payment);
        _stockMainRepository.Update(stockMain);
        await _unitOfWork.SaveChangesAsync();

        return payment;
    }

    public async Task<Payment> CreateWalkingCustomerReceiptAsync(Payment payment, int userId)
    {
        // Validate the transaction exists
        var stockMain = await _stockMainRepository.Query()
            .FirstOrDefaultAsync(s => s.StockMainID == payment.StockMain_ID);

        if (stockMain == null)
            throw new InvalidOperationException("Transaction not found.");

        if (payment.Amount <= 0)
            throw new InvalidOperationException("Receipt amount must be greater than zero.");

        if (payment.Amount > stockMain.BalanceAmount)
            throw new InvalidOperationException($"Receipt amount ({payment.Amount:N2}) exceeds balance ({stockMain.BalanceAmount:N2}).");

        // Get the receipt account (Cash/Bank)
        var receiptAccount = await _accountRepository.Query()
            .FirstOrDefaultAsync(a => a.AccountID == payment.Account_ID);

        if (receiptAccount == null)
            throw new InvalidOperationException("Receipt account not found.");

        // Get walking customer account from configuration
        var walkingCustomerAccount = await _accountRepository.Query()
            .FirstOrDefaultAsync(a => a.AccountID == _systemAccountSettings.WalkingCustomerAccountId);

        if (walkingCustomerAccount == null)
            throw new InvalidOperationException("Walking customer account not configured. Please check SystemAccounts settings.");

        // Generate reference number
        payment.Reference = await GenerateReferenceNoAsync();
        payment.PaymentType = "RECEIPT"; // Customer receipt
        payment.CreatedAt = DateTime.Now;
        payment.CreatedBy = userId;

        // Create accounting voucher
        var voucher = await CreateReceiptVoucherAsync(
            payment, 
            walkingCustomerAccount, 
            receiptAccount, 
            "Walking Customer", 
            userId);

        payment.Voucher_ID = voucher.VoucherID;

        // Update transaction balance
        stockMain.PaidAmount += payment.Amount;
        stockMain.BalanceAmount = stockMain.TotalAmount - stockMain.PaidAmount;
        stockMain.PaymentStatus = stockMain.BalanceAmount <= 0 ? "Paid" : "Partial";
        stockMain.UpdatedAt = DateTime.Now;
        stockMain.UpdatedBy = userId;

        await _paymentRepository.AddAsync(payment);
        _stockMainRepository.Update(stockMain);
        await _unitOfWork.SaveChangesAsync();

        return payment;
    }

    /// <summary>
    /// Creates a receipt voucher with double-entry accounting.
    /// Debit: Cash/Bank Account - increases asset
    /// Credit: Customer Account (Accounts Receivable) - reduces asset (what they owe us)
    /// </summary>
    private async Task<Voucher> CreateReceiptVoucherAsync(
        Payment payment, 
        Account customerAccount, 
        Account cashBankAccount,
        string customerName,
        int userId)
    {
        // Get Receipt Voucher type
        var voucherType = await _voucherTypeRepository.Query()
            .FirstOrDefaultAsync(vt => vt.Code == VOUCHER_TYPE_CODE);

        if (voucherType == null)
            throw new InvalidOperationException($"Voucher type '{VOUCHER_TYPE_CODE}' not found. Please ensure it exists in the database.");

        var voucherNo = await GenerateVoucherNoAsync();

        var voucher = new Voucher
        {
            VoucherType_ID = voucherType.VoucherTypeID,
            VoucherNo = voucherNo,
            VoucherDate = payment.PaymentDate,
            TotalDebit = payment.Amount,
            TotalCredit = payment.Amount,
            Status = "Posted",
            SourceTable = "Payment",
            SourceID = null,
            Narration = $"Receipt from customer: {customerName}. Ref: {payment.Reference}",
            CreatedAt = DateTime.Now,
            CreatedBy = userId,
            VoucherDetails = new List<VoucherDetail>
            {
                // Debit: Cash/Bank Account - increases asset
                new VoucherDetail
                {
                    Account_ID = cashBankAccount.AccountID,
                    DebitAmount = payment.Amount,
                    CreditAmount = 0,
                    Description = $"Receipt via {payment.PaymentMethod}"
                },
                // Credit: Customer Account (Accounts Receivable) - reduces what they owe us
                new VoucherDetail
                {
                    Account_ID = customerAccount.AccountID,
                    DebitAmount = 0,
                    CreditAmount = payment.Amount,
                    Description = $"Receipt from {customerName}",
                    Party_ID = payment.Party_ID
                }
            }
        };

        await _voucherRepository.AddAsync(voucher);
        await _unitOfWork.SaveChangesAsync();

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

    private async Task<string> GenerateVoucherNoAsync()
    {
        var datePrefix = $"RV-{DateTime.Now:yyyyMMdd}-";

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
}
