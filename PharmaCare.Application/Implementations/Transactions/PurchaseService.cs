using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Domain.Entities.Accounting;
using PharmaCare.Domain.Entities.Configuration;
using PharmaCare.Domain.Entities.Finance;
using PharmaCare.Domain.Entities.Transactions;
using PharmaCare.Domain.Enums;

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
    private const string BANK_PAYMENT_VOUCHER_CODE = "BP";

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
        return await ExecuteInTransactionAsync(async () =>
        {
            // Get the GRN transaction type
            var transactionType = await _transactionTypeRepository.Query()
                .FirstOrDefaultAsync(t => t.Code == TRANSACTION_TYPE_CODE);

            if (transactionType == null)
                throw new InvalidOperationException($"Transaction type '{TRANSACTION_TYPE_CODE}' not found.");

            if (!purchase.Party_ID.HasValue || purchase.Party_ID.Value <= 0)
            {
                throw new InvalidOperationException("Supplier is required.");
            }

            StockMain? referencePo = null;
            var poPaymentsToTransfer = new List<Payment>();
            if (purchase.ReferenceStockMain_ID.HasValue)
            {
                referencePo = await _stockMainRepository.Query()
                    .Include(s => s.TransactionType)
                    .Include(s => s.StockDetails)
                    .FirstOrDefaultAsync(s => s.StockMainID == purchase.ReferenceStockMain_ID.Value);

                if (referencePo == null ||
                    !string.Equals(referencePo.TransactionType?.Code, PO_TRANSACTION_TYPE_CODE, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Selected reference transaction is not a valid Purchase Order.");
                }

                if (!string.Equals(referencePo.Status, "Approved", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Only approved Purchase Orders can be used for GRN.");
                }

                if (referencePo.Party_ID != purchase.Party_ID)
                {
                    throw new InvalidOperationException("Selected Purchase Order belongs to a different supplier.");
                }

                await ValidateGrnAgainstPurchaseOrderAsync(purchase, referencePo);

                poPaymentsToTransfer = await _paymentRepository.Query()
                    .Where(p => p.StockMain_ID == referencePo.StockMainID && p.PaymentType == PaymentType.PAYMENT.ToString())
                    .ToListAsync();
            }

            if (!purchase.ReferenceStockMain_ID.HasValue && transferredAdvanceAmount > 0)
            {
                throw new InvalidOperationException("Advance transfer amount can only be used when a reference PO is selected.");
            }

            var availablePoAdvanceAmount = poPaymentsToTransfer.Sum(p => p.Amount);
            var requestedTransferAmount = Math.Max(0, Math.Round(transferredAdvanceAmount, 2));
            if (requestedTransferAmount > availablePoAdvanceAmount)
            {
                throw new InvalidOperationException(
                    $"Requested advance transfer ({requestedTransferAmount:N2}) exceeds available PO advance ({availablePoAdvanceAmount:N2}).");
            }

            NormalizePurchaseLines(purchase);

            purchase.TransactionType_ID = transactionType.TransactionTypeID;
            purchase.TransactionNo = await GenerateTransactionNoAsync(PREFIX);
            purchase.Status = "Approved"; // GRN is immediately approved (stock impact)
            purchase.CreatedAt = DateTime.Now;
            purchase.CreatedBy = userId;

            // Calculate totals
            CalculateTotals(purchase);

            if (requestedTransferAmount > purchase.TotalAmount)
            {
                throw new InvalidOperationException(
                    $"Requested advance transfer ({requestedTransferAmount:N2}) cannot exceed GRN total ({purchase.TotalAmount:N2}).");
            }

            if (purchase.PaidAmount < requestedTransferAmount)
            {
                throw new InvalidOperationException("Paid amount cannot be less than the advance transfer amount.");
            }

            if (purchase.PaidAmount > purchase.TotalAmount)
            {
                throw new InvalidOperationException("Paid amount cannot exceed GRN total amount.");
            }

            var additionalPaymentAmount = purchase.PaidAmount - requestedTransferAmount;
            if (additionalPaymentAmount > 0 && !paymentAccountId.HasValue)
            {
                throw new InvalidOperationException("Payment account is required when additional payment is entered.");
            }

            purchase.BalanceAmount = Math.Max(0, purchase.TotalAmount - purchase.PaidAmount);
            purchase.PaymentStatus = CalculatePaymentStatus(purchase.PaidAmount, purchase.BalanceAmount);

            await _stockMainRepository.AddAsync(purchase);
            await _unitOfWork.SaveChangesAsync();

            // Create accounting entries for the purchase
            var purchaseVoucher = await CreatePurchaseVoucherAsync(purchase, userId);
            purchase.Voucher = purchaseVoucher;

            // If this GRN is created from a PO (ReferenceStockMain_ID is present),
            // transfer advance payments from the PO to this GRN.
            decimal transferredFromPo = 0;
            if (referencePo != null)
            {
                if (requestedTransferAmount > 0)
                {
                    transferredFromPo = await TransferPoAdvancePaymentsAsync(
                        referencePo,
                        purchase,
                        requestedTransferAmount,
                        userId);
                }

                if (transferredFromPo > 0)
                {
                    referencePo.PaidAmount = Math.Max(0, referencePo.PaidAmount - transferredFromPo);
                    referencePo.BalanceAmount = Math.Max(0, referencePo.TotalAmount - referencePo.PaidAmount);
                    referencePo.PaymentStatus = CalculatePaymentStatus(referencePo.PaidAmount, referencePo.BalanceAmount);
                    referencePo.UpdatedAt = DateTime.Now;
                    referencePo.UpdatedBy = userId;
                    _stockMainRepository.Update(referencePo);
                }
            }

            purchase.PaidAmount = additionalPaymentAmount + transferredFromPo;

            purchase.BalanceAmount = Math.Max(0, purchase.TotalAmount - purchase.PaidAmount);
            purchase.PaymentStatus = CalculatePaymentStatus(purchase.PaidAmount, purchase.BalanceAmount);

            var previousBalance = await GetSupplierBalanceAsync(purchase.Party_ID ?? 0, purchase.StockMainID);
            if (previousBalance < 0)
            {
                var advanceAvailable = Math.Abs(previousBalance);
                var remainingUnpaid = purchase.TotalAmount - purchase.PaidAmount;

                if (remainingUnpaid > 0)
                {
                    var deductionAmount = Math.Min(advanceAvailable, remainingUnpaid);
                    if (deductionAmount > 0)
                    {
                        await CreateAdjustmentVoucherAsync(purchase, userId, deductionAmount);

                        purchase.PaidAmount += deductionAmount;
                        purchase.BalanceAmount = Math.Max(0, purchase.TotalAmount - purchase.PaidAmount);
                        purchase.PaymentStatus = CalculatePaymentStatus(purchase.PaidAmount, purchase.BalanceAmount);
                        purchase.Remarks += $"; Adjusted {deductionAmount:N2} from Advance.";
                    }
                }
            }

            // If a NEW payment was made directly during creation (as indicated by paymentAccountId)
            if (additionalPaymentAmount > 0)
            {
                await CreatePaymentVoucherAsync(purchase, userId, paymentAccountId!.Value, additionalPaymentAmount);
            }

            _stockMainRepository.Update(purchase);
            await _unitOfWork.SaveChangesAsync();

            return purchase;
        });
    }

    private async Task<decimal> TransferPoAdvancePaymentsAsync(
        StockMain referencePo,
        StockMain purchase,
        decimal requestedTransferAmount,
        int userId)
    {
        if (requestedTransferAmount <= 0)
        {
            return 0;
        }

        var poPayments = await _paymentRepository.Query()
            .Where(p => p.StockMain_ID == referencePo.StockMainID && p.PaymentType == PaymentType.PAYMENT.ToString())
            .OrderBy(p => p.PaymentDate)
            .ThenBy(p => p.PaymentID)
            .ToListAsync();

        var availableAmount = poPayments.Sum(p => p.Amount);
        if (requestedTransferAmount > availableAmount)
        {
            throw new InvalidOperationException(
                $"Requested advance transfer ({requestedTransferAmount:N2}) exceeds available PO advance ({availableAmount:N2}).");
        }

        decimal transferredAmount = 0;
        var remainingAmount = requestedTransferAmount;

        foreach (var payment in poPayments)
        {
            if (remainingAmount <= 0)
            {
                break;
            }

            var moveAmount = Math.Min(payment.Amount, remainingAmount);
            if (moveAmount <= 0)
            {
                continue;
            }

            var originalRemarks = payment.Remarks;
            var transferNote = $"Transferred {moveAmount:N2} from PO {referencePo.TransactionNo} to GRN {purchase.TransactionNo}";

            if (Math.Abs(moveAmount - payment.Amount) < 0.0001m)
            {
                payment.StockMain_ID = purchase.StockMainID;
                payment.Remarks = string.IsNullOrWhiteSpace(originalRemarks)
                    ? transferNote
                    : $"{originalRemarks} ({transferNote})";
                _paymentRepository.Update(payment);

                if (payment.Voucher_ID.HasValue)
                {
                    var linkedVoucher = await _voucherRepository.Query()
                        .FirstOrDefaultAsync(v => v.VoucherID == payment.Voucher_ID.Value);

                    if (linkedVoucher != null)
                    {
                        linkedVoucher.SourceTable = "StockMain";
                        linkedVoucher.SourceID = purchase.StockMainID;
                        linkedVoucher.Narration = string.IsNullOrWhiteSpace(linkedVoucher.Narration)
                            ? transferNote
                            : $"{linkedVoucher.Narration} ({transferNote})";
                        _voucherRepository.Update(linkedVoucher);
                    }
                }
            }
            else
            {
                var transferredVoucherId = await SplitPaymentVoucherForTransferAsync(
                    payment,
                    moveAmount,
                    purchase.StockMainID,
                    transferNote,
                    userId);

                payment.Amount = Math.Round(payment.Amount - moveAmount, 2);
                payment.Remarks = string.IsNullOrWhiteSpace(originalRemarks)
                    ? $"Partially transferred {moveAmount:N2} to GRN {purchase.TransactionNo}"
                    : $"{originalRemarks} (Partially transferred {moveAmount:N2} to GRN {purchase.TransactionNo})";
                _paymentRepository.Update(payment);

                var transferredPayment = new Payment
                {
                    PaymentType = payment.PaymentType,
                    Party_ID = payment.Party_ID,
                    StockMain_ID = purchase.StockMainID,
                    Account_ID = payment.Account_ID,
                    Amount = Math.Round(moveAmount, 2),
                    PaymentDate = payment.PaymentDate,
                    PaymentMethod = payment.PaymentMethod,
                    Reference = payment.Reference,
                    ChequeNo = payment.ChequeNo,
                    ChequeDate = payment.ChequeDate,
                    Remarks = transferNote,
                    Voucher_ID = transferredVoucherId,
                    CreatedAt = DateTime.Now,
                    CreatedBy = userId
                };

                await _paymentRepository.AddAsync(transferredPayment);
            }

            transferredAmount += moveAmount;
            remainingAmount -= moveAmount;
        }

        return Math.Round(transferredAmount, 2);
    }

    private async Task<int?> SplitPaymentVoucherForTransferAsync(
        Payment originalPayment,
        decimal moveAmount,
        int newSourceStockMainId,
        string transferNote,
        int userId)
    {
        if (!originalPayment.Voucher_ID.HasValue || moveAmount <= 0)
        {
            return originalPayment.Voucher_ID;
        }

        var originalVoucher = await _voucherRepository.Query()
            .Include(v => v.VoucherType)
            .Include(v => v.VoucherDetails)
            .FirstOrDefaultAsync(v => v.VoucherID == originalPayment.Voucher_ID.Value);

        if (originalVoucher == null)
        {
            return originalPayment.Voucher_ID;
        }

        var originalAmount = originalPayment.Amount;
        if (moveAmount >= originalAmount)
        {
            originalVoucher.SourceTable = "StockMain";
            originalVoucher.SourceID = newSourceStockMainId;
            originalVoucher.Narration = string.IsNullOrWhiteSpace(originalVoucher.Narration)
                ? transferNote
                : $"{originalVoucher.Narration} ({transferNote})";
            _voucherRepository.Update(originalVoucher);
            return originalVoucher.VoucherID;
        }

        var remainingAmount = Math.Round(originalAmount - moveAmount, 2);
        var ratio = originalAmount <= 0 ? 0 : moveAmount / originalAmount;

        var transferredVoucherNo = await GenerateVoucherNoAsync(originalVoucher.VoucherType?.Code ?? CASH_PAYMENT_VOUCHER_CODE);
        var transferredVoucher = new Voucher
        {
            VoucherType_ID = originalVoucher.VoucherType_ID,
            VoucherNo = transferredVoucherNo,
            VoucherDate = originalVoucher.VoucherDate,
            TotalDebit = Math.Round(moveAmount, 2),
            TotalCredit = Math.Round(moveAmount, 2),
            Status = originalVoucher.Status,
            SourceTable = "StockMain",
            SourceID = newSourceStockMainId,
            Narration = transferNote,
            CreatedAt = DateTime.Now,
            CreatedBy = userId
        };

        foreach (var detail in originalVoucher.VoucherDetails)
        {
            var moveDebit = Math.Round(detail.DebitAmount * ratio, 2);
            var moveCredit = Math.Round(detail.CreditAmount * ratio, 2);

            detail.DebitAmount = Math.Round(detail.DebitAmount - moveDebit, 2);
            detail.CreditAmount = Math.Round(detail.CreditAmount - moveCredit, 2);

            transferredVoucher.VoucherDetails.Add(new VoucherDetail
            {
                Account_ID = detail.Account_ID,
                DebitAmount = moveDebit,
                CreditAmount = moveCredit,
                Description = detail.Description,
                Party_ID = detail.Party_ID,
                Product_ID = detail.Product_ID
            });
        }

        originalVoucher.TotalDebit = Math.Round(remainingAmount, 2);
        originalVoucher.TotalCredit = Math.Round(remainingAmount, 2);
        _voucherRepository.Update(originalVoucher);
        await _voucherRepository.AddAsync(transferredVoucher);
        await _unitOfWork.SaveChangesAsync();

        return transferredVoucher.VoucherID;
    }

    private static void NormalizePurchaseLines(StockMain purchase)
    {
        if (purchase.StockDetails == null || purchase.StockDetails.Count == 0)
        {
            throw new InvalidOperationException("At least one item is required.");
        }

        foreach (var detail in purchase.StockDetails)
        {
            if (detail.Quantity <= 0)
            {
                throw new InvalidOperationException("Each line item must have a quantity greater than zero.");
            }

            var unitRate = detail.CostPrice > 0 ? detail.CostPrice : detail.UnitPrice;
            if (unitRate < 0)
            {
                throw new InvalidOperationException("Cost price cannot be negative.");
            }

            var grossAmount = Math.Round(detail.Quantity * unitRate, 2);
            var lineDiscount = detail.DiscountPercent > 0
                ? Math.Round(grossAmount * detail.DiscountPercent / 100, 2)
                : Math.Round(Math.Max(0, detail.DiscountAmount), 2);

            if (lineDiscount > grossAmount)
            {
                throw new InvalidOperationException("Line discount cannot exceed line amount.");
            }

            detail.CostPrice = unitRate;
            detail.UnitPrice = unitRate;
            detail.DiscountAmount = lineDiscount;
            detail.LineTotal = Math.Round(grossAmount - lineDiscount, 2);
            detail.LineCost = Math.Round(detail.Quantity * detail.CostPrice, 2);
        }
    }

    private async Task CreateAdjustmentVoucherAsync(StockMain purchase, int userId, decimal amount)
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
            
            PaymentType = PaymentType.ADJUSTMENT.ToString(),
            Party_ID = purchase.Party_ID ?? 0,
            StockMain_ID = purchase.StockMainID,
            Account_ID = supplier?.Account_ID ?? 0, // Just link to supplier account? or null?
            Amount = amount,
            PaymentDate = purchase.TransactionDate,
            PaymentMethod = PaymentMethod.Adjustment.ToString(),
            Reference = paymentReference + "-ADJ",
            Remarks = $"Adjusted against Advance for {purchase.TransactionNo}",
            CreatedAt = DateTime.Now,
            CreatedBy = userId
        };

        await _paymentRepository.AddAsync(payment);
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

        return voucher;
    }

    /// <summary>
    /// Creates a Cash Payment Voucher (CP) with double-entry accounting and a Payment record.
    /// Debit: Supplier Account - reduces liability
    /// Credit: Cash/Bank Account - reduces asset
    /// </summary>
    private async Task<Voucher> CreatePaymentVoucherAsync(StockMain purchase, int userId, int accountId, decimal amount)
    {
        // Get supplier with account
        var supplier = await _partyRepository.Query()
            .Include(p => p.Account)
            .FirstOrDefaultAsync(p => p.PartyID == purchase.Party_ID);

        if (supplier?.Account_ID == null)
            throw new InvalidOperationException("Supplier does not have an associated account.");

        // Get the selected cash/bank account
        var cashBankAccount = await _accountRepository.Query()
            .Include(a => a.AccountType)
            .FirstOrDefaultAsync(a => a.AccountID == accountId);
        if (cashBankAccount == null)
            throw new InvalidOperationException("Selected payment account not found.");

        var isBankLikeAccount =
            string.Equals(cashBankAccount.AccountType?.Code, "BANK", StringComparison.OrdinalIgnoreCase) ||
            cashBankAccount.AccountType?.Name?.Contains("Bank", StringComparison.OrdinalIgnoreCase) == true;

        var voucherTypeCode = isBankLikeAccount ? BANK_PAYMENT_VOUCHER_CODE : CASH_PAYMENT_VOUCHER_CODE;
        var voucherType = await _voucherTypeRepository.Query()
            .FirstOrDefaultAsync(vt => vt.Code == voucherTypeCode);

        if (voucherType == null)
            throw new InvalidOperationException($"Voucher type '{voucherTypeCode}' not found.");

        var voucherNo = await GenerateVoucherNoAsync(voucherTypeCode);
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

        // Create the Payment record
        var payment = new Payment
        {
            PaymentType = PaymentType.PAYMENT.ToString(), // Money to supplier
            Party_ID = supplier.PartyID,
            StockMain_ID = purchase.StockMainID,
            Account_ID = cashBankAccount.AccountID,
            Amount = amount,
            PaymentDate = purchase.TransactionDate,
            PaymentMethod = isBankLikeAccount ? PaymentMethod.Bank.ToString() : PaymentMethod.Cash.ToString(),
            Reference = paymentReference,
            Remarks = $"Initial payment for purchase {purchase.TransactionNo}",
            Voucher = voucher,
            CreatedAt = DateTime.Now,
            CreatedBy = userId
        };

        await _paymentRepository.AddAsync(payment);

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
            .AsNoTracking()
            .Include(s => s.TransactionType)
            .Include(s => s.Party)
            .Include(s => s.StockDetails)
                .ThenInclude(d => d.Product)
            .Where(s => s.TransactionType!.Code == PO_TRANSACTION_TYPE_CODE && s.Status == "Approved");

        if (supplierId.HasValue)
        {
            query = query.Where(s => s.Party_ID == supplierId.Value);
        }

        var purchaseOrders = await query
            .OrderByDescending(s => s.TransactionDate)
            .ToListAsync();

        if (purchaseOrders.Count == 0)
        {
            return purchaseOrders;
        }

        var poIds = purchaseOrders.Select(po => po.StockMainID).ToList();

        var receivedLines = await _stockMainRepository.Query()
            .Include(s => s.TransactionType)
            .Where(s => s.TransactionType!.Code == TRANSACTION_TYPE_CODE
                     && s.Status != "Void"
                     && s.ReferenceStockMain_ID.HasValue
                     && poIds.Contains(s.ReferenceStockMain_ID.Value))
            .SelectMany(s => s.StockDetails.Select(d => new
            {
                PoId = s.ReferenceStockMain_ID!.Value,
                d.Product_ID,
                d.Quantity
            }))
            .ToListAsync();

        var receivedLookup = receivedLines
            .GroupBy(x => new { x.PoId, x.Product_ID })
            .ToDictionary(
                g => (g.Key.PoId, g.Key.Product_ID),
                g => g.Sum(x => x.Quantity));

        var availablePurchaseOrders = new List<StockMain>();
        foreach (var po in purchaseOrders)
        {
            var remainingDetails = new List<StockDetail>();
            foreach (var group in po.StockDetails.GroupBy(d => d.Product_ID))
            {
                var orderedQty = group.Sum(d => d.Quantity);
                receivedLookup.TryGetValue((po.StockMainID, group.Key), out var receivedQty);
                var remainingQty = orderedQty - receivedQty;
                if (remainingQty <= 0)
                {
                    continue;
                }

                var firstDetail = group.First();
                remainingDetails.Add(new StockDetail
                {
                    Product_ID = firstDetail.Product_ID,
                    Product = firstDetail.Product,
                    Quantity = remainingQty,
                    CostPrice = firstDetail.CostPrice,
                    UnitPrice = firstDetail.CostPrice,
                    LineTotal = Math.Round(remainingQty * firstDetail.CostPrice, 2),
                    LineCost = Math.Round(remainingQty * firstDetail.CostPrice, 2)
                });
            }

            if (remainingDetails.Count == 0)
            {
                continue;
            }

            po.StockDetails = remainingDetails;
            po.SubTotal = remainingDetails.Sum(d => d.LineTotal);
            po.TotalAmount = po.SubTotal;
            availablePurchaseOrders.Add(po);
        }

        return availablePurchaseOrders;
    }

    public async Task<bool> VoidAsync(int id, string reason, int userId)
    {
        return await ExecuteInTransactionAsync(async () =>
        {
            var purchase = await _stockMainRepository.Query()
                .Include(s => s.TransactionType)
                .FirstOrDefaultAsync(s => s.StockMainID == id && s.TransactionType!.Code == TRANSACTION_TYPE_CODE);

            if (purchase == null || purchase.Status == "Void")
                return false;

            purchase.Status = "Void";
            purchase.VoidReason = reason;
            purchase.VoidedAt = DateTime.Now;
            purchase.VoidedBy = userId;

            // Reverse all posted vouchers linked to this StockMain (purchase + payments, if any)
            await CreateReversalVouchersForSourceAsync("StockMain", purchase.StockMainID, userId, reason);

            _stockMainRepository.Update(purchase);
            await _unitOfWork.SaveChangesAsync();

            return true;
        });
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
            .Include(p => p.StockMain)
            .Where(p => p.Party_ID == supplierId
                        && p.PaymentType == PaymentType.PAYMENT.ToString()
                        && (!p.StockMain_ID.HasValue || p.StockMain == null || p.StockMain.Status != "Void"))
            .SumAsync(p => p.Amount);

        return balance + purchases - returns - payments;
    }

    private async Task ValidateGrnAgainstPurchaseOrderAsync(StockMain purchase, StockMain purchaseOrder)
    {
        if (purchase.StockDetails == null || purchase.StockDetails.Count == 0)
        {
            throw new InvalidOperationException("At least one item is required for GRN.");
        }

        var orderedByProduct = purchaseOrder.StockDetails
            .GroupBy(d => d.Product_ID)
            .ToDictionary(g => g.Key, g => g.Sum(d => d.Quantity));

        var requestedByProduct = purchase.StockDetails
            .GroupBy(d => d.Product_ID)
            .ToDictionary(g => g.Key, g => g.Sum(d => d.Quantity));

        var invalidProducts = requestedByProduct.Keys
            .Where(productId => !orderedByProduct.ContainsKey(productId))
            .ToList();

        if (invalidProducts.Count > 0)
        {
            throw new InvalidOperationException("GRN contains item(s) that are not present in the selected Purchase Order.");
        }

        var receivedLines = await _stockMainRepository.Query()
            .Include(s => s.TransactionType)
            .Where(s => s.TransactionType!.Code == TRANSACTION_TYPE_CODE
                     && s.Status != "Void"
                     && s.ReferenceStockMain_ID == purchaseOrder.StockMainID)
            .SelectMany(s => s.StockDetails.Select(d => new
            {
                d.Product_ID,
                d.Quantity
            }))
            .ToListAsync();

        var alreadyReceivedByProduct = receivedLines
            .GroupBy(x => x.Product_ID)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

        foreach (var requested in requestedByProduct)
        {
            var productId = requested.Key;
            var orderedQty = orderedByProduct[productId];
            alreadyReceivedByProduct.TryGetValue(productId, out var alreadyReceivedQty);
            var requestedQty = requested.Value;

            if (alreadyReceivedQty + requestedQty > orderedQty)
            {
                throw new InvalidOperationException(
                    $"GRN quantity for product ID {productId} exceeds PO quantity. " +
                    $"Ordered: {orderedQty:N4}, Already Received: {alreadyReceivedQty:N4}, Current GRN: {requestedQty:N4}.");
            }
        }
    }

    private static string CalculatePaymentStatus(decimal paidAmount, decimal balanceAmount)
    {
        if (balanceAmount <= 0)
        {
            return PaymentStatus.Paid.ToString();
        }

        return paidAmount <= 0 ? PaymentStatus.Unpaid.ToString() : PaymentStatus.Partial.ToString();
    }
}
