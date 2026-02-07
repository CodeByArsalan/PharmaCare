using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Domain.Entities.Accounting;
using PharmaCare.Domain.Entities.Configuration;
using PharmaCare.Domain.Entities.Finance;
using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.Application.Implementations.Transactions;

/// <summary>
/// Service for managing Purchases (GRN - Goods Received Notes).
/// Creates accounting vouchers for double-entry bookkeeping.
/// </summary>
public class PurchaseService : IPurchaseService
{
    private readonly IRepository<StockMain> _stockMainRepository;
    private readonly IRepository<TransactionType> _transactionTypeRepository;
    private readonly IRepository<Voucher> _voucherRepository;
    private readonly IRepository<VoucherType> _voucherTypeRepository;
    private readonly IRepository<Party> _partyRepository;
    private readonly IRepository<Product> _productRepository;
    private readonly IRepository<Account> _accountRepository;
    private readonly IRepository<Payment> _paymentRepository;
    private readonly IUnitOfWork _unitOfWork;

    private const string TRANSACTION_TYPE_CODE = "GRN";
    private const string PO_TRANSACTION_TYPE_CODE = "PO";
    private const string PREFIX = "GRN";
    private const string PURCHASE_VOUCHER_CODE = "PV";
    private const string CASH_PAYMENT_VOUCHER_CODE = "CP";
    private const int DEFAULT_CASH_ACCOUNT_ID = 1; // Cash in Hand

    public PurchaseService(
        IRepository<StockMain> stockMainRepository,
        IRepository<TransactionType> transactionTypeRepository,
        IRepository<Voucher> voucherRepository,
        IRepository<VoucherType> voucherTypeRepository,
        IRepository<Party> partyRepository,
        IRepository<Product> productRepository,
        IRepository<Account> accountRepository,
        IRepository<Payment> paymentRepository,
        IUnitOfWork unitOfWork)
    {
        _stockMainRepository = stockMainRepository;
        _transactionTypeRepository = transactionTypeRepository;
        _voucherRepository = voucherRepository;
        _voucherTypeRepository = voucherTypeRepository;
        _partyRepository = partyRepository;
        _productRepository = productRepository;
        _accountRepository = accountRepository;
        _paymentRepository = paymentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<StockMain>> GetAllAsync()
    {
        return await _stockMainRepository.Query()
            .Include(s => s.TransactionType)
            .Include(s => s.Party)
            .Include(s => s.ReferenceStockMain)
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
            .Include(s => s.ReferenceStockMain)
            .Include(s => s.StockDetails)
                .ThenInclude(d => d.Product)
            .FirstOrDefaultAsync(s => s.StockMainID == id && s.TransactionType!.Code == TRANSACTION_TYPE_CODE);
    }

    public async Task<StockMain> CreateAsync(StockMain purchase, int userId, int? paymentAccountId = null)
    {
        // Get the GRN transaction type
        var transactionType = await _transactionTypeRepository.Query()
            .FirstOrDefaultAsync(t => t.Code == TRANSACTION_TYPE_CODE);

        if (transactionType == null)
            throw new InvalidOperationException($"Transaction type '{TRANSACTION_TYPE_CODE}' not found.");

        purchase.TransactionType_ID = transactionType.TransactionTypeID;
        purchase.TransactionNo = await GenerateTransactionNoAsync();
        purchase.Status = "Approved"; // GRN is immediately approved (stock impact)
        purchase.PaymentStatus = purchase.PaidAmount > 0 
            ? (purchase.PaidAmount >= purchase.TotalAmount ? "Paid" : "Partial") 
            : "Unpaid";
        purchase.CreatedAt = DateTime.Now;
        purchase.CreatedBy = userId;

        // Calculate totals
        CalculateTotals(purchase);

        await _stockMainRepository.AddAsync(purchase);
        await _unitOfWork.SaveChangesAsync();

        // Create accounting entries
        await CreatePurchaseVoucherAsync(purchase, userId);

        // If payment was made, create payment voucher
        if (purchase.PaidAmount > 0)
        {
            // Use provided account or default to Cash in Hand (ID: 1)
            var accountId = paymentAccountId ?? DEFAULT_CASH_ACCOUNT_ID;
            await CreatePaymentVoucherAsync(purchase, userId, accountId);
        }

        return purchase;
    }

    /// <summary>
    /// Creates a Purchase Voucher (PV) with double-entry accounting.
    /// Debit: Stock Account(s) - increases inventory asset
    /// Credit: Supplier Account (Accounts Payable) - increases liability
    /// </summary>
    private async Task<Voucher> CreatePurchaseVoucherAsync(StockMain purchase, int userId)
    {
        // Get Purchase Voucher type
        var voucherType = await _voucherTypeRepository.Query()
            .FirstOrDefaultAsync(vt => vt.Code == PURCHASE_VOUCHER_CODE);

        if (voucherType == null)
            throw new InvalidOperationException($"Voucher type '{PURCHASE_VOUCHER_CODE}' not found.");

        // Get supplier with account
        var supplier = await _partyRepository.Query()
            .Include(p => p.Account)
            .FirstOrDefaultAsync(p => p.PartyID == purchase.Party_ID);

        if (supplier?.Account_ID == null)
            throw new InvalidOperationException("Supplier does not have an associated account for accounting entries.");

        // Get products with their categories and stock accounts
        var productIds = purchase.StockDetails.Select(d => d.Product_ID).Distinct().ToList();
        var products = await _productRepository.Query()
            .Include(p => p.Category)
            .Where(p => productIds.Contains(p.ProductID))
            .ToListAsync();

        // Group line items by stock account and sum totals
        var stockAccountTotals = new Dictionary<int, decimal>();
        foreach (var detail in purchase.StockDetails)
        {
            var product = products.FirstOrDefault(p => p.ProductID == detail.Product_ID);
            var stockAccountId = product?.Category?.StockAccount_ID;
            
            if (stockAccountId == null)
                throw new InvalidOperationException($"Product '{product?.Name}' does not have a stock account configured in its category.");

            if (stockAccountTotals.ContainsKey(stockAccountId.Value))
                stockAccountTotals[stockAccountId.Value] += detail.LineTotal;
            else
                stockAccountTotals[stockAccountId.Value] = detail.LineTotal;
        }

        var voucherNo = await GenerateVoucherNoAsync(PURCHASE_VOUCHER_CODE);

        var voucher = new Voucher
        {
            VoucherType_ID = voucherType.VoucherTypeID,
            VoucherNo = voucherNo,
            VoucherDate = purchase.TransactionDate,
            TotalDebit = purchase.TotalAmount,
            TotalCredit = purchase.TotalAmount,
            Status = "Posted",
            SourceTable = "StockMain",
            SourceID = purchase.StockMainID,
            Narration = $"Purchase from {supplier.Name}. GRN: {purchase.TransactionNo}",
            CreatedAt = DateTime.Now,
            CreatedBy = userId
        };

        // Add debit lines for each stock account
        foreach (var stockAccount in stockAccountTotals)
        {
            voucher.VoucherDetails.Add(new VoucherDetail
            {
                Account_ID = stockAccount.Key,
                DebitAmount = stockAccount.Value,
                CreditAmount = 0,
                Description = $"Inventory purchase - {purchase.TransactionNo}"
            });
        }

        // Add credit line for supplier account
        voucher.VoucherDetails.Add(new VoucherDetail
        {
            Account_ID = supplier.Account_ID.Value,
            DebitAmount = 0,
            CreditAmount = purchase.TotalAmount,
            Description = $"Purchase from {supplier.Name}",
            Party_ID = supplier.PartyID
        });

        await _voucherRepository.AddAsync(voucher);
        await _unitOfWork.SaveChangesAsync();

        return voucher;
    }

    /// <summary>
    /// Creates a Cash Payment Voucher (CP) with double-entry accounting and a Payment record.
    /// Debit: Supplier Account - reduces liability
    /// Credit: Cash/Bank Account - reduces asset
    /// </summary>
    private async Task<Voucher> CreatePaymentVoucherAsync(StockMain purchase, int userId, int accountId)
    {
        // Get Cash Payment Voucher type
        var voucherType = await _voucherTypeRepository.Query()
            .FirstOrDefaultAsync(vt => vt.Code == CASH_PAYMENT_VOUCHER_CODE);

        if (voucherType == null)
            throw new InvalidOperationException($"Voucher type '{CASH_PAYMENT_VOUCHER_CODE}' not found.");

        // Get supplier with account
        var supplier = await _partyRepository.Query()
            .Include(p => p.Account)
            .FirstOrDefaultAsync(p => p.PartyID == purchase.Party_ID);

        if (supplier?.Account_ID == null)
            throw new InvalidOperationException("Supplier does not have an associated account.");

        // Get the selected cash/bank account
        var cashBankAccount = await _accountRepository.GetByIdAsync(accountId);
        if (cashBankAccount == null)
            throw new InvalidOperationException("Selected payment account not found.");

        var voucherNo = await GenerateVoucherNoAsync(CASH_PAYMENT_VOUCHER_CODE);
        var paymentReference = await GeneratePaymentReferenceAsync();

        // Create the voucher
        var voucher = new Voucher
        {
            VoucherType_ID = voucherType.VoucherTypeID,
            VoucherNo = voucherNo,
            VoucherDate = purchase.TransactionDate,
            TotalDebit = purchase.PaidAmount,
            TotalCredit = purchase.PaidAmount,
            Status = "Posted",
            SourceTable = "StockMain",
            SourceID = purchase.StockMainID,
            Narration = $"Payment against purchase {purchase.TransactionNo} to {supplier.Name}",
            CreatedAt = DateTime.Now,
            CreatedBy = userId,
            VoucherDetails = new List<VoucherDetail>
            {
                // Debit: Supplier Account - reduces liability
                new VoucherDetail
                {
                    Account_ID = supplier.Account_ID.Value,
                    DebitAmount = purchase.PaidAmount,
                    CreditAmount = 0,
                    Description = $"Payment to {supplier.Name}",
                    Party_ID = supplier.PartyID
                },
                // Credit: Cash/Bank Account - reduces asset
                new VoucherDetail
                {
                    Account_ID = cashBankAccount.AccountID,
                    DebitAmount = 0,
                    CreditAmount = purchase.PaidAmount,
                    Description = $"Payment via {cashBankAccount.Name} for {purchase.TransactionNo}"
                }
            }
        };

        await _voucherRepository.AddAsync(voucher);
        await _unitOfWork.SaveChangesAsync();

        // Create the Payment record
        var payment = new Payment
        {
            PaymentType = "PAYMENT", // Money to supplier
            Party_ID = supplier.PartyID,
            StockMain_ID = purchase.StockMainID,
            Account_ID = cashBankAccount.AccountID,
            Amount = purchase.PaidAmount,
            PaymentDate = purchase.TransactionDate,
            PaymentMethod = cashBankAccount.AccountType?.Code == "BANK" ? "Bank" : "Cash",
            Reference = paymentReference,
            Remarks = $"Initial payment for purchase {purchase.TransactionNo}",
            Voucher_ID = voucher.VoucherID,
            CreatedAt = DateTime.Now,
            CreatedBy = userId
        };

        await _paymentRepository.AddAsync(payment);
        await _unitOfWork.SaveChangesAsync();

        return voucher;
    }

    private async Task<string> GeneratePaymentReferenceAsync()
    {
        var prefix = $"PAY-{DateTime.Now:yyyyMMdd}-";

        var lastPayment = await _paymentRepository.Query()
            .Where(p => p.Reference != null && p.Reference.StartsWith(prefix))
            .OrderByDescending(p => p.Reference)
            .FirstOrDefaultAsync();

        int nextNum = 1;
        if (lastPayment?.Reference != null)
        {
            var parts = lastPayment.Reference.Split('-');
            if (parts.Length > 2 && int.TryParse(parts.Last(), out int lastNum))
            {
                nextNum = lastNum + 1;
            }
        }

        return $"{prefix}{nextNum:D4}";
    }

    public async Task<IEnumerable<StockMain>> GetPurchaseOrdersForGrnAsync(int? supplierId = null)
    {
        var query = _stockMainRepository.Query()
            .Include(s => s.TransactionType)
            .Include(s => s.Party)
            .Include(s => s.StockDetails)
                .ThenInclude(d => d.Product)
            .Where(s => s.TransactionType!.Code == PO_TRANSACTION_TYPE_CODE && s.Status == "Approved");

        if (supplierId.HasValue)
        {
            query = query.Where(s => s.Party_ID == supplierId.Value);
        }

        return await query
            .OrderByDescending(s => s.TransactionDate)
            .ToListAsync();
    }

    public async Task<bool> VoidAsync(int id, string reason, int userId)
    {
        var purchase = await GetByIdAsync(id);
        if (purchase == null)
            return false;

        if (purchase.Status == "Void")
            return false;

        purchase.Status = "Void";
        purchase.VoidReason = reason;
        purchase.VoidedAt = DateTime.Now;
        purchase.VoidedBy = userId;

        _stockMainRepository.Update(purchase);
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

    private async Task<string> GenerateVoucherNoAsync(string prefix)
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

    private void CalculateTotals(StockMain purchase)
    {
        purchase.SubTotal = purchase.StockDetails.Sum(d => d.LineTotal);

        if (purchase.DiscountPercent > 0)
        {
            purchase.DiscountAmount = Math.Round(purchase.SubTotal * purchase.DiscountPercent / 100, 2);
        }

        purchase.TotalAmount = purchase.SubTotal - purchase.DiscountAmount;
        purchase.BalanceAmount = purchase.TotalAmount - purchase.PaidAmount;
    }
}
