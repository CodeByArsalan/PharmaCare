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
    private const string SALE_RETURN_TRANSACTION_TYPE_CODE = "SRTN";
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
                     && s.Status == "Approved");

        if (customerId.HasValue)
        {
            query = query.Where(s => s.Party_ID == customerId.Value);
        }

        var sales = await query
            .OrderByDescending(s => s.TransactionDate)
            .ToListAsync();

        if (!sales.Any())
            return sales;

        var saleIds = sales.Select(s => s.StockMainID).ToList();
        var returnSums = await _stockMainRepository.Query()
            .Include(s => s.TransactionType)
            .Where(s => s.TransactionType!.Code == SALE_RETURN_TRANSACTION_TYPE_CODE
                     && s.ReferenceStockMain_ID.HasValue
                     && saleIds.Contains(s.ReferenceStockMain_ID.Value)
                     && s.Status != "Void")
            .GroupBy(s => s.ReferenceStockMain_ID!.Value)
            .Select(g => new
            {
                SaleId = g.Key,
                TotalReturns = g.Sum(x => x.TotalAmount)
            })
            .ToListAsync();

        var returnMap = returnSums.ToDictionary(x => x.SaleId, x => x.TotalReturns);

        foreach (var sale in sales)
        {
            var totalReturns = returnMap.TryGetValue(sale.StockMainID, out var amount) ? amount : 0;
            sale.BalanceAmount = sale.TotalAmount - sale.PaidAmount - totalReturns;
            sale.PaymentStatus = sale.BalanceAmount <= 0
                ? "Paid"
                : (sale.PaidAmount > 0 ? "Partial" : "Unpaid");
        }

        return sales.Where(s => s.BalanceAmount > 0).ToList();
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

        var outstandingBeforeReceipt = await GetNetOutstandingAmountAsync(stockMain.StockMainID, stockMain.TotalAmount, stockMain.PaidAmount);
        if (outstandingBeforeReceipt <= 0)
            throw new InvalidOperationException("This sale has no outstanding receivable after considering sale returns.");

        if (payment.Amount > outstandingBeforeReceipt)
            throw new InvalidOperationException($"Receipt amount ({payment.Amount:N2}) exceeds balance ({outstandingBeforeReceipt:N2}).");

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
        await RecalculateSaleBalanceIncludingReturnsAsync(stockMain, userId);

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

        var outstandingBeforeReceipt = await GetNetOutstandingAmountAsync(stockMain.StockMainID, stockMain.TotalAmount, stockMain.PaidAmount);
        if (outstandingBeforeReceipt <= 0)
            throw new InvalidOperationException("This sale has no outstanding receivable after considering sale returns.");

        if (payment.Amount > outstandingBeforeReceipt)
            throw new InvalidOperationException($"Receipt amount ({payment.Amount:N2}) exceeds balance ({outstandingBeforeReceipt:N2}).");

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
        await RecalculateSaleBalanceIncludingReturnsAsync(stockMain, userId);

        await _paymentRepository.AddAsync(payment);
        _stockMainRepository.Update(stockMain);
        await _unitOfWork.SaveChangesAsync();

        return payment;
    }

    private async Task<decimal> GetNetOutstandingAmountAsync(int saleId, decimal totalAmount, decimal paidAmount)
    {
        var totalReturns = await _stockMainRepository.Query()
            .Include(s => s.TransactionType)
            .Where(s => s.TransactionType!.Code == SALE_RETURN_TRANSACTION_TYPE_CODE
                     && s.ReferenceStockMain_ID == saleId
                     && s.Status != "Void")
            .SumAsync(s => (decimal?)s.TotalAmount) ?? 0;

        return totalAmount - paidAmount - totalReturns;
    }

    private async Task RecalculateSaleBalanceIncludingReturnsAsync(StockMain sale, int userId)
    {
        sale.BalanceAmount = await GetNetOutstandingAmountAsync(sale.StockMainID, sale.TotalAmount, sale.PaidAmount);
        sale.PaymentStatus = sale.BalanceAmount <= 0
            ? "Paid"
            : (sale.PaidAmount > 0 ? "Partial" : "Unpaid");
        sale.UpdatedAt = DateTime.Now;
        sale.UpdatedBy = userId;
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
        // Get Receipt Voucher type (fallback to JV if RV not found)
        var voucherType = await _voucherTypeRepository.Query()
            .FirstOrDefaultAsync(vt => vt.Code == VOUCHER_TYPE_CODE || vt.Code == "JV");

        if (voucherType == null)
            throw new InvalidOperationException($"Voucher type '{VOUCHER_TYPE_CODE}' or 'JV' not found. Please ensure it exists in the database.");

        // Determine voucher prefix based on account type (CR for Cash, BR for Bank)
        string voucherPrefix = "RV";
        if (cashBankAccount.AccountType_ID == 1) // Cash
        {
            voucherPrefix = "CR";
        }
        else if (cashBankAccount.AccountType_ID == 2) // Bank
        {
            voucherPrefix = "BR";
        }

        var voucherNo = await GenerateVoucherNoAsync(voucherPrefix);

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

    private async Task<string> GenerateVoucherNoAsync(string prefix = "RV")
    {
        var datePrefix = $"{prefix}-{DateTime.Now:yyyyMMdd}-";

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
    /// Creates a refund to a customer.
    /// DR: Customer Account (A/R) - restores receivable / reduces credit balance
    /// CR: Cash/Bank Account - cash goes out
    /// </summary>
    public async Task<Payment> CreateRefundAsync(Payment payment, int userId)
    {
        if (payment.Amount <= 0)
            throw new InvalidOperationException("Refund amount must be greater than zero.");

        if (payment.Party_ID <= 0)
            throw new InvalidOperationException("Customer is required for a refund.");

        var customer = await _partyRepository.Query()
            .Include(p => p.Account)
            .FirstOrDefaultAsync(p => p.PartyID == payment.Party_ID);

        if (customer == null)
            throw new InvalidOperationException("Customer not found.");

        if (customer.Account_ID == null || customer.Account == null)
            throw new InvalidOperationException($"Customer '{customer.Name}' does not have a linked account.");

        var customerAccount = customer.Account;

        var refundAccount = await _accountRepository.Query()
            .FirstOrDefaultAsync(a => a.AccountID == payment.Account_ID);

        if (refundAccount == null)
            throw new InvalidOperationException("Refund account not found.");

        payment.Reference = await GenerateReferenceNoAsync();
        payment.PaymentType = "REFUND";
        payment.StockMain_ID = null;
        payment.Remarks = string.IsNullOrWhiteSpace(payment.Remarks)
            ? $"Refund to {customer.Name}"
            : payment.Remarks;
        payment.CreatedAt = DateTime.Now;
        payment.CreatedBy = userId;

        var voucherType = await _voucherTypeRepository.Query()
            .FirstOrDefaultAsync(vt => vt.Code == VOUCHER_TYPE_CODE || vt.Code == "JV");

        if (voucherType == null)
            throw new InvalidOperationException("Voucher type not found.");

        string voucherPrefix = refundAccount.AccountType_ID == 1 ? "CP" : "BP";
        var voucherNo = await GenerateVoucherNoAsync(voucherPrefix);

        var voucher = new Voucher
        {
            VoucherType_ID = voucherType.VoucherTypeID,
            VoucherNo = voucherNo,
            VoucherDate = payment.PaymentDate,
            TotalDebit = payment.Amount,
            TotalCredit = payment.Amount,
            Status = "Posted",
            SourceTable = "Payment",
            Narration = $"Customer refund to {customer.Name}. Ref: {payment.Reference}",
            CreatedAt = DateTime.Now,
            CreatedBy = userId,
            VoucherDetails = new List<VoucherDetail>
            {
                new VoucherDetail
                {
                    Account_ID = customerAccount.AccountID,
                    DebitAmount = payment.Amount,
                    CreditAmount = 0,
                    Description = $"Refund to {customer.Name}",
                    Party_ID = payment.Party_ID
                },
                new VoucherDetail
                {
                    Account_ID = refundAccount.AccountID,
                    DebitAmount = 0,
                    CreditAmount = payment.Amount,
                    Description = $"Refund via {payment.PaymentMethod}"
                }
            }
        };

        await _voucherRepository.AddAsync(voucher);
        await _unitOfWork.SaveChangesAsync();

        payment.Voucher_ID = voucher.VoucherID;
        await _paymentRepository.AddAsync(payment);
        await _unitOfWork.SaveChangesAsync();

        return payment;
    }

    /// <summary>
    /// Gets all customer refunds.
    /// </summary>
    public async Task<IEnumerable<Payment>> GetAllRefundsAsync()
    {
        return await _paymentRepository.Query()
            .Include(p => p.Party)
            .Include(p => p.Account)
            .Where(p => p.PaymentType == "REFUND")
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.PaymentID)
            .ToListAsync();
    }
}
