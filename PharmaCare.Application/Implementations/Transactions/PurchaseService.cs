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
public class PurchaseService : TransactionServiceBase, IPurchaseService
{
    private readonly IRepository<TransactionType> _transactionTypeRepository;
    private readonly IRepository<VoucherType> _voucherTypeRepository;
    private readonly IRepository<Party> _partyRepository;
    private readonly IRepository<Product> _productRepository;
    private readonly IRepository<Account> _accountRepository;
    private readonly IRepository<Payment> _paymentRepository;

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
        : base(stockMainRepository, voucherRepository, unitOfWork)
    {
        _transactionTypeRepository = transactionTypeRepository;
        _voucherTypeRepository = voucherTypeRepository;
        _partyRepository = partyRepository;
        _productRepository = productRepository;
        _accountRepository = accountRepository;
        _paymentRepository = paymentRepository;
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

    public async Task<StockMain> CreateAsync(
        StockMain purchase,
        int userId,
        int? paymentAccountId = null,
        decimal transferredAdvanceAmount = 0)
    {
        // Get the GRN transaction type
        var transactionType = await _transactionTypeRepository.Query()
            .FirstOrDefaultAsync(t => t.Code == TRANSACTION_TYPE_CODE);

        if (transactionType == null)
            throw new InvalidOperationException($"Transaction type '{TRANSACTION_TYPE_CODE}' not found.");

        if (!purchase.ReferenceStockMain_ID.HasValue)
        {
            transferredAdvanceAmount = 0;
        }

        var additionalPaymentAmount = Math.Max(0, purchase.PaidAmount - transferredAdvanceAmount);
        purchase.PaidAmount = additionalPaymentAmount;

        if (additionalPaymentAmount > 0 && !paymentAccountId.HasValue)
        {
            throw new InvalidOperationException("Payment account is required when additional payment is entered.");
        }

        purchase.TransactionType_ID = transactionType.TransactionTypeID;
        purchase.TransactionNo = await GenerateTransactionNoAsync(PREFIX);
        purchase.Status = "Approved"; // GRN is immediately approved (stock impact)
        purchase.CreatedAt = DateTime.Now;
        purchase.CreatedBy = userId;

        // Calculate totals
        CalculateTotals(purchase);
        purchase.PaymentStatus = purchase.PaidAmount > 0
            ? (purchase.PaidAmount >= purchase.TotalAmount ? "Paid" : "Partial")
            : "Unpaid";

        await _stockMainRepository.AddAsync(purchase);
        await _unitOfWork.SaveChangesAsync();

        // Create accounting entries for the purchase
        var purchaseVoucher = await CreatePurchaseVoucherAsync(purchase, userId);
        purchase.Voucher_ID = purchaseVoucher.VoucherID;

        // If this GRN is created from a PO (ReferenceStockMain_ID is present)
        // We must transfer any advance payments from the PO to this GRN.
        decimal transferredFromPo = 0;
        if (purchase.ReferenceStockMain_ID.HasValue)
        {
            var poPayments = await _paymentRepository.Query()
                .Where(p => p.StockMain_ID == purchase.ReferenceStockMain_ID.Value && p.PaymentType == "PAYMENT")
                .ToListAsync();

            if (poPayments.Any())
            {
                foreach (var payment in poPayments)
                {
                    // Relink payment to this new GRN
                    payment.StockMain_ID = purchase.StockMainID;
                    payment.Remarks += $" (Transferred from PO {purchase.ReferenceStockMain?.TransactionNo ?? ""})";
                    _paymentRepository.Update(payment);

                    transferredFromPo += payment.Amount;
                }
            }
        }

        purchase.PaidAmount = additionalPaymentAmount + transferredFromPo;

        // Update totals and status with the accumulated paid amount
        purchase.BalanceAmount = purchase.TotalAmount - purchase.PaidAmount;
        purchase.PaymentStatus = purchase.PaidAmount > 0
            ? (purchase.PaidAmount >= purchase.TotalAmount ? "Paid" : "Partial")
            : "Unpaid";

        _stockMainRepository.Update(purchase);
        await _unitOfWork.SaveChangesAsync();

        // ---------------------------------------------------------
        // AUTOMATIC SUPPLIER ADVANCE DEDUCTION
        // ---------------------------------------------------------
        
        // REVISED STRATEGY: 
        // We now have a deterministic way to get "Balance Before this Purchase" 
        // by excluding the current StockMainID from the sum.
        
        // This returns the balance effectively "Before" this purchase was added to the sum.
        var previousBalance = await GetSupplierBalanceAsync(purchase.Party_ID ?? 0, purchase.StockMainID);
        
        // If previousBalance is Negative, we have an Advance.
        if (previousBalance < 0)
        {
            var advanceAvailable = Math.Abs(previousBalance);
            
            // We can settle up to the remaining unpaid amount of this purchase
            var remainingUnpaid = purchase.TotalAmount - purchase.PaidAmount;
            
            if (remainingUnpaid > 0)
            {
                var deductionAmount = Math.Min(advanceAvailable, remainingUnpaid);
                
                if (deductionAmount > 0)
                {
                    // Create Adjustment Payment
                    var adjustmentVoucher = await CreateAdjustmentVoucherAsync(purchase, userId, deductionAmount);
                    
                    purchase.PaidAmount += deductionAmount;
                    purchase.BalanceAmount = purchase.TotalAmount - purchase.PaidAmount;
                    purchase.PaymentStatus = purchase.PaidAmount >= purchase.TotalAmount ? "Paid" : "Partial";
                    purchase.Remarks += $"; Adjusted {deductionAmount:N2} from Advance.";
                    
                    _stockMainRepository.Update(purchase);
                    await _unitOfWork.SaveChangesAsync();
                }
            }
        }

        // If a NEW payment was made directly during creation (as indicated by paymentAccountId)
        if (additionalPaymentAmount > 0)
        {
            await CreatePaymentVoucherAsync(purchase, userId, paymentAccountId!.Value, additionalPaymentAmount);
        }

        return purchase;
    }

    private async Task<Voucher> CreateAdjustmentVoucherAsync(StockMain purchase, int userId, decimal amount)
    {
        // For Adjustment, we are essentially saying "We paid this using our Advance".
        // Accounting Entry:
        // Debit: Supplier A/C (Decrease Liability logic? No, wait.)
        // When we made Advance, we did: Debit Supplier, Credit Cash.
        // Supplier Balance is Debit (Advance).
        // Now we made Purchase: Debit Stock, Credit Supplier. (Supplier Bal: -5000 + 3000 = -2000).
        // The Net Balance is already correct in the Ledger! 
        
        // So, we create a Payment record for tracking "This invoice was paid by..." 
        // BUT we must ensure `ReportService` Party Ledger does NOT sum "ADJUSTMENT" type payments either!
        
        var paymentReference = await GeneratePaymentReferenceAsync();
        var supplier = await _partyRepository.GetByIdAsync(purchase.Party_ID ?? 0);
        
        var payment = new Payment
        {
            // If I change Type to "ADJUSTMENT", it won't be picked up by:
            // - PurchaseService.GetSupplierBalanceAsync (Good!)
            // - ReportService.GetPartyLedgerAsync (Good!)
            // - ReportService.CashFlow (Good!)
            
            PaymentType = "ADJUSTMENT", 
            Party_ID = purchase.Party_ID ?? 0,
            StockMain_ID = purchase.StockMainID,
            Account_ID = supplier?.Account_ID ?? 0, // Just link to supplier account? or null?
            Amount = amount,
            PaymentDate = purchase.TransactionDate,
            PaymentMethod = "ADJUSTMENT",
            Reference = paymentReference + "-ADJ",
            Remarks = $"Adjusted against Advance for {purchase.TransactionNo}",
            CreatedAt = DateTime.Now,
            CreatedBy = userId
        };

        await _paymentRepository.AddAsync(payment);
        await _unitOfWork.SaveChangesAsync();
        
        return null; // No voucher
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
    private async Task<Voucher> CreatePaymentVoucherAsync(StockMain purchase, int userId, int accountId, decimal amount)
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
            TotalDebit = amount,
            TotalCredit = amount,
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
                    DebitAmount = amount,
                    CreditAmount = 0,
                    Description = $"Payment to {supplier.Name}",
                    Party_ID = supplier.PartyID
                },
                // Credit: Cash/Bank Account - reduces asset
                new VoucherDetail
                {
                    Account_ID = cashBankAccount.AccountID,
                    DebitAmount = 0,
                    CreditAmount = amount,
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
            Amount = amount,
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
        var grnTypeId = await _transactionTypeRepository.Query()
            .Where(t => t.Code == TRANSACTION_TYPE_CODE)
            .Select(t => t.TransactionTypeID)
            .FirstOrDefaultAsync();

        if (grnTypeId == 0)
        {
            throw new InvalidOperationException($"Transaction type '{TRANSACTION_TYPE_CODE}' not found.");
        }

        var query = _stockMainRepository.Query()
            .Include(s => s.TransactionType)
            .Include(s => s.Party)
            .Include(s => s.StockDetails)
                .ThenInclude(d => d.Product)
            .Where(s => s.TransactionType!.Code == PO_TRANSACTION_TYPE_CODE && s.Status == "Approved")
            .Where(po => !_stockMainRepository.Query()
                .Any(grn =>
                    grn.TransactionType_ID == grnTypeId &&
                    grn.ReferenceStockMain_ID == po.StockMainID &&
                    grn.Status != "Void"));

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
        var purchase = await _stockMainRepository.Query()
              .Include(s => s.TransactionType)
              .Include(s => s.Voucher)
                  .ThenInclude(v => v!.VoucherDetails)
              .FirstOrDefaultAsync(s => s.StockMainID == id && s.TransactionType!.Code == TRANSACTION_TYPE_CODE);

        if (purchase == null)
            return false;

        if (purchase.Status == "Void")
            return false;

        purchase.Status = "Void";
        purchase.VoidReason = reason;
        purchase.VoidedAt = DateTime.Now;
        purchase.VoidedBy = userId;

        // Create reversing voucher for the purchase voucher
        if (purchase.Voucher != null && purchase.Voucher.VoucherDetails.Any())
        {
            await CreateReversalVoucherAsync(purchase.Voucher.VoucherID, userId, reason, "StockMain", purchase.StockMainID);
        }

        _stockMainRepository.Update(purchase);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }

    private async Task<decimal> GetSupplierBalanceAsync(int supplierId, int? excludeTransactionId = null)
    {
        var supplier = await _partyRepository.GetByIdAsync(supplierId);
        if (supplier == null) return 0;
        
        decimal balance = supplier.OpeningBalance;

        // Purchases (Credit)
        var purchasesQuery = _stockMainRepository.Query()
            .Include(s => s.TransactionType)
            .Where(s => s.Party_ID == supplierId 
                        && s.TransactionType!.Code == TRANSACTION_TYPE_CODE 
                        && s.Status != "Void");

        if (excludeTransactionId.HasValue)
        {
            purchasesQuery = purchasesQuery.Where(s => s.StockMainID != excludeTransactionId.Value);
        }

        var purchases = await purchasesQuery.SumAsync(s => s.TotalAmount);
            
        // Purchase Returns (Debit)
        var returns = await _stockMainRepository.Query()
            .Include(s => s.TransactionType)
            .Where(s => s.Party_ID == supplierId 
                        && s.TransactionType!.Code == "PRTN" 
                        && s.Status != "Void")
            .SumAsync(s => s.TotalAmount);
            
        // Payments (Debit)
        var payments = await _paymentRepository.Query()
            .Where(p => p.Party_ID == supplierId && p.PaymentType == "PAYMENT")
            .SumAsync(p => p.Amount);

        return balance + purchases - returns - payments;
    }
}
