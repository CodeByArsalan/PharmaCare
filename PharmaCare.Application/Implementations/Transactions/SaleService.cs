using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Application.Settings;
using PharmaCare.Domain.Entities.Accounting;
using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.Application.Implementations.Transactions;

/// <summary>
/// Service for managing Sales with double-entry accounting.
/// </summary>
public class SaleService : ISaleService
{
    private readonly IRepository<StockMain> _stockMainRepository;
    private readonly IRepository<TransactionType> _transactionTypeRepository;
    private readonly IRepository<Voucher> _voucherRepository;
    private readonly IRepository<VoucherType> _voucherTypeRepository;
    private readonly IRepository<Account> _accountRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly SystemAccountSettings _systemAccounts;

    private const string TRANSACTION_TYPE_CODE = "SALE";
    private const string PREFIX = "SALE";
    private const string VOUCHER_TYPE_CODE = "SV"; // Sales Voucher

    public SaleService(
        IRepository<StockMain> stockMainRepository,
        IRepository<TransactionType> transactionTypeRepository,
        IRepository<Voucher> voucherRepository,
        IRepository<VoucherType> voucherTypeRepository,
        IRepository<Account> accountRepository,
        IUnitOfWork unitOfWork,
        IOptions<SystemAccountSettings> systemAccountSettings)
    {
        _stockMainRepository = stockMainRepository;
        _transactionTypeRepository = transactionTypeRepository;
        _voucherRepository = voucherRepository;
        _voucherTypeRepository = voucherTypeRepository;
        _accountRepository = accountRepository;
        _unitOfWork = unitOfWork;
        _systemAccounts = systemAccountSettings.Value;
    }

    public async Task<IEnumerable<StockMain>> GetAllAsync()
    {
        return await _stockMainRepository.Query()
            .Include(s => s.TransactionType)
            .Include(s => s.Party)
            .Where(s => s.TransactionType!.Code == TRANSACTION_TYPE_CODE)
            .OrderByDescending(s => s.TransactionDate)
            .ThenByDescending(s => s.StockMainID)
            .ToListAsync();
    }

    public async Task<StockMain?> GetByIdAsync(int id)
    {
        return await _stockMainRepository.Query()
            .Include(s => s.TransactionType)
            .Include(s => s.Party)
            .Include(s => s.StockDetails)
                .ThenInclude(d => d.Product)
            .Include(s => s.Voucher)
            .FirstOrDefaultAsync(s => s.StockMainID == id && s.TransactionType!.Code == TRANSACTION_TYPE_CODE);
    }

    public async Task<StockMain> CreateAsync(StockMain sale, int userId)
    {
        // Get the SALE transaction type
        var transactionType = await _transactionTypeRepository.Query()
            .FirstOrDefaultAsync(t => t.Code == TRANSACTION_TYPE_CODE);

        if (transactionType == null)
            throw new InvalidOperationException($"Transaction type '{TRANSACTION_TYPE_CODE}' not found.");

        sale.TransactionType_ID = transactionType.TransactionTypeID;
        sale.TransactionNo = await GenerateTransactionNoAsync();
        sale.Status = "Approved"; // Sales are immediately approved (stock impact)
        sale.CreatedAt = DateTime.Now;
        sale.CreatedBy = userId;

        // Calculate totals
        CalculateTotals(sale);

        // Set payment status based on paid amount
        if (sale.PaidAmount >= sale.TotalAmount)
        {
            sale.PaymentStatus = "Paid";
        }
        else if (sale.PaidAmount > 0)
        {
            sale.PaymentStatus = "Partial";
        }
        else
        {
            sale.PaymentStatus = "Unpaid";
        }

        // Create accounting voucher for the sale
        var voucher = await CreateSaleVoucherAsync(sale, userId);
        sale.Voucher_ID = voucher.VoucherID;

        await _stockMainRepository.AddAsync(sale);
        await _unitOfWork.SaveChangesAsync();

        return sale;
    }

    /// <summary>
    /// Creates a sales voucher with double-entry accounting.
    /// For Sale:
    ///   Debit: Customer Account (AR) - what they owe us
    ///   Credit: Sales Revenue Account - income
    /// 
    /// If immediate payment is made:
    ///   Debit: Cash Account - money received
    ///   Credit: Customer Account (AR) - reduces what they owe
    /// </summary>
    private async Task<Voucher> CreateSaleVoucherAsync(StockMain sale, int userId)
    {
        // Get Sales Voucher type (or create a Journal Voucher if SV doesn't exist)
        var voucherType = await _voucherTypeRepository.Query()
            .FirstOrDefaultAsync(vt => vt.Code == VOUCHER_TYPE_CODE || vt.Code == "JV");

        if (voucherType == null)
            throw new InvalidOperationException($"Voucher type '{VOUCHER_TYPE_CODE}' or 'JV' not found. Please ensure it exists in the database.");

        // Get Sales Revenue account
        var salesRevenueAccount = await _accountRepository.Query()
            .FirstOrDefaultAsync(a => a.AccountID == _systemAccounts.SalesRevenueAccountId);

        if (salesRevenueAccount == null)
            throw new InvalidOperationException($"Sales Revenue account (ID: {_systemAccounts.SalesRevenueAccountId}) not found. Please configure SystemAccounts:SalesRevenueAccountId in appsettings.json.");

        // Get customer account (either from Party or use Walking Customer account)
        Account customerAccount;
        string customerName;

        if (sale.Party_ID.HasValue && sale.Party?.Account != null)
        {
            customerAccount = sale.Party.Account;
            customerName = sale.Party.Name;
        }
        else
        {
            // Use Walking Customer account
            var walkingCustomerAccount = await _accountRepository.Query()
                .FirstOrDefaultAsync(a => a.AccountID == _systemAccounts.WalkingCustomerAccountId);

            if (walkingCustomerAccount == null)
                throw new InvalidOperationException($"Walking Customer account (ID: {_systemAccounts.WalkingCustomerAccountId}) not found. Please configure SystemAccounts:WalkingCustomerAccountId in appsettings.json.");

            customerAccount = walkingCustomerAccount;
            customerName = "Walk-in Customer";
        }

        var voucherNo = await GenerateVoucherNoAsync();

        var voucherDetails = new List<VoucherDetail>
        {
            // Debit: Customer Account (Accounts Receivable) - what customer owes us
            new VoucherDetail
            {
                Account_ID = customerAccount.AccountID,
                DebitAmount = sale.TotalAmount,
                CreditAmount = 0,
                Description = $"Sale to {customerName}",
                Party_ID = sale.Party_ID
            },
            // Credit: Sales Revenue Account - income earned
            new VoucherDetail
            {
                Account_ID = salesRevenueAccount.AccountID,
                DebitAmount = 0,
                CreditAmount = sale.TotalAmount,
                Description = $"Sales Revenue - {sale.TransactionNo}"
            }
        };

        // If immediate payment is made, add payment entries
        if (sale.PaidAmount > 0)
        {
            var cashAccount = await _accountRepository.Query()
                .FirstOrDefaultAsync(a => a.AccountID == _systemAccounts.CashAccountId);

            if (cashAccount == null)
                throw new InvalidOperationException($"Cash account (ID: {_systemAccounts.CashAccountId}) not found. Please configure SystemAccounts:CashAccountId in appsettings.json.");

            // Debit: Cash Account - money received
            voucherDetails.Add(new VoucherDetail
            {
                Account_ID = cashAccount.AccountID,
                DebitAmount = sale.PaidAmount,
                CreditAmount = 0,
                Description = $"Cash received for sale {sale.TransactionNo}"
            });

            // Credit: Customer Account - reduces receivable
            voucherDetails.Add(new VoucherDetail
            {
                Account_ID = customerAccount.AccountID,
                DebitAmount = 0,
                CreditAmount = sale.PaidAmount,
                Description = $"Payment received from {customerName}",
                Party_ID = sale.Party_ID
            });
        }

        var voucher = new Voucher
        {
            VoucherType_ID = voucherType.VoucherTypeID,
            VoucherNo = voucherNo,
            VoucherDate = sale.TransactionDate,
            TotalDebit = sale.TotalAmount + sale.PaidAmount,
            TotalCredit = sale.TotalAmount + sale.PaidAmount,
            Status = "Posted",
            SourceTable = "StockMain",
            SourceID = null, // Will be set after sale is saved
            Narration = $"Sale to {customerName}. Invoice: {sale.TransactionNo}",
            CreatedAt = DateTime.Now,
            CreatedBy = userId,
            VoucherDetails = voucherDetails
        };

        await _voucherRepository.AddAsync(voucher);
        await _unitOfWork.SaveChangesAsync();

        return voucher;
    }

    public async Task<bool> VoidAsync(int id, string reason, int userId)
    {
        var sale = await GetByIdAsync(id);
        if (sale == null)
            return false;

        if (sale.Status == "Void")
            return false;

        sale.Status = "Void";
        sale.VoidReason = reason;
        sale.VoidedAt = DateTime.Now;
        sale.VoidedBy = userId;

        // TODO: Create reversing journal entry for voided sale

        _stockMainRepository.Update(sale);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }

    private async Task<string> GenerateTransactionNoAsync()
    {
        var datePrefix = $"{PREFIX}-{DateTime.Now:yyyyMMdd}-";

        var lastTransaction = await _stockMainRepository.Query()
            .Where(s => s.TransactionNo.StartsWith(datePrefix))
            .OrderByDescending(s => s.TransactionNo)
            .FirstOrDefaultAsync();

        int nextNum = 1;
        if (lastTransaction != null)
        {
            var parts = lastTransaction.TransactionNo.Split('-');
            if (parts.Length > 2 && int.TryParse(parts.Last(), out int lastNum))
            {
                nextNum = lastNum + 1;
            }
        }

        return $"{datePrefix}{nextNum:D4}";
    }

    private async Task<string> GenerateVoucherNoAsync()
    {
        var datePrefix = $"SV-{DateTime.Now:yyyyMMdd}-";

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

    private void CalculateTotals(StockMain sale)
    {
        sale.SubTotal = sale.StockDetails.Sum(d => d.LineTotal);

        if (sale.DiscountPercent > 0)
        {
            sale.DiscountAmount = Math.Round(sale.SubTotal * sale.DiscountPercent / 100, 2);
        }

        sale.TotalAmount = sale.SubTotal - sale.DiscountAmount;
        sale.BalanceAmount = sale.TotalAmount - sale.PaidAmount;
    }
}
