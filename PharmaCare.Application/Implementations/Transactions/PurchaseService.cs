using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Application.Utilities;
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
    private readonly IRepository<PaymentAllocation> _allocationRepository;
    private readonly IRepository<SupplierCreditNote> _supplierCreditNoteRepository;
    private readonly IProductService _productService;

    private const string TRANSACTION_TYPE_CODE = "GRN";
    private const string PO_TRANSACTION_TYPE_CODE = "PO";
    private const string PREFIX = "GRN";
    private const string PURCHASE_VOUCHER_CODE = "PV";
    private const string CASH_PAYMENT_VOUCHER_CODE = "CP";
    private const string BANK_PAYMENT_VOUCHER_CODE = "BP";
    private const string SupplierCreditAllocationSource = "SupplierCredit";

    public PurchaseService(
        IRepository<StockMain> stockMainRepository,
        IRepository<TransactionType> transactionTypeRepository,
        IRepository<Voucher> voucherRepository,
        IRepository<VoucherType> voucherTypeRepository,
        IRepository<Party> partyRepository,
        IRepository<Product> productRepository,
        IRepository<Account> accountRepository,
        IRepository<Payment> paymentRepository,
        IRepository<PaymentAllocation> allocationRepository,
        IRepository<SupplierCreditNote> supplierCreditNoteRepository,
        IProductService productService,
        IUnitOfWork unitOfWork,
        IFinancialPeriodService financialPeriodService)
        : base(stockMainRepository, voucherRepository, unitOfWork, financialPeriodService)
    {
        _transactionTypeRepository = transactionTypeRepository;
        _voucherTypeRepository = voucherTypeRepository;
        _partyRepository = partyRepository;
        _productRepository = productRepository;
        _accountRepository = accountRepository;
        _paymentRepository = paymentRepository;
        _allocationRepository = allocationRepository;
        _supplierCreditNoteRepository = supplierCreditNoteRepository;
        _productService = productService;
    }

    /// <summary>
    /// Returns any supplier-credit-note value allocated to <paramref name="purchase"/> back to the
    /// notes it came from, and retires the allocation rows. Used when a GRN is voided so the credit
    /// stays spendable against a future purchase.
    /// </summary>
    private async Task RestoreSupplierCreditNotesAsync(StockMain purchase, int userId)
    {
        var allocations = await _allocationRepository.Query()
            .Include(a => a.SupplierCreditNote)
            .Where(a => a.StockMain_ID == purchase.StockMainID
                     && a.SourceType == SupplierCreditAllocationSource
                     && a.SupplierCreditNote_ID != null)
            .ToListAsync();

        foreach (var allocation in allocations)
        {
            var note = allocation.SupplierCreditNote;
            if (note != null)
            {
                note.AppliedAmount -= allocation.Amount;
                note.BalanceAmount += allocation.Amount;
                note.Status = note.BalanceAmount > 0 ? "Open" : "Applied";
                note.UpdatedAt = AppTime.Now;
                note.UpdatedBy = userId;
                _supplierCreditNoteRepository.Update(note);
            }

            _allocationRepository.Remove(allocation);
        }
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

    public async Task<PharmaCare.Application.DTOs.PagedResult<StockMain>> GetPagedAsync(
        int? partyId, DateTime? fromDate, DateTime? toDate, string? status, int page, int pageSize)
    {
        var query = _stockMainRepository.Query()
            .Include(s => s.TransactionType)
            .Include(s => s.Party)
            .Include(s => s.ReferenceStockMain)
            .Where(s => s.TransactionType!.Code == TRANSACTION_TYPE_CODE);

        if (partyId.HasValue && partyId.Value > 0)
            query = query.Where(s => s.Party_ID == partyId.Value);

        if (fromDate.HasValue)
            query = query.Where(s => s.TransactionDate >= fromDate.Value.Date);

        if (toDate.HasValue)
            query = query.Where(s => s.TransactionDate <= toDate.Value.Date.AddDays(1).AddTicks(-1));

        if (!string.IsNullOrEmpty(status) && status != "All")
            query = query.Where(s => s.Status == status);

        int totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(s => s.TransactionDate)
            .ThenByDescending(s => s.StockMainID)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PharmaCare.Application.DTOs.PagedResult<StockMain>
        {
            Items = items,
            TotalCount = totalCount,
            CurrentPage = page,
            PageSize = pageSize
        };
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
        await ValidatePeriodAsync(purchase.TransactionDate);
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

            await ValidateSupplierAsync(purchase.Party_ID.Value);

            StockMain? referencePo = null;
            var poPaymentsToTransfer = new List<Payment>();
            if (purchase.ReferenceStockMain_ID.HasValue)
            {
                // Serialize every receipt against this PO. The remaining-quantity check below is
                // read-then-write, and a GRN that does not complete the PO never stamps the PO row,
                // so nothing else would collide: two receipts each within the remaining quantity
                // could both commit and jointly over-receive the order.
                await _unitOfWork.AcquireResourceLockAsync($"po:{purchase.ReferenceStockMain_ID.Value}");

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
                    .Where(p => p.StockMain_ID == referencePo.StockMainID
                                && p.PaymentType == PaymentType.PAYMENT.ToString()
                                && !p.IsVoided)
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
            purchase.CreatedAt = AppTime.Now;
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
                    referencePo.UpdatedAt = AppTime.Now;
                    referencePo.UpdatedBy = userId;
                    _stockMainRepository.Update(referencePo);
                }
            }

            purchase.PaidAmount = additionalPaymentAmount + transferredFromPo;

            purchase.BalanceAmount = Math.Max(0, purchase.TotalAmount - purchase.PaidAmount);
            purchase.PaymentStatus = CalculatePaymentStatus(purchase.PaidAmount, purchase.BalanceAmount);

            // The transfer above re-pointed the PO's payments at this GRN in memory only. The
            // balance below is a database query, so without flushing first it still sees those
            // payments sitting on the PO, reads the money we just applied here as a second free
            // advance, and books a phantom adjustment for it. Still inside the same transaction.
            if (transferredFromPo > 0)
            {
                await _unitOfWork.SaveChangesAsync();
            }

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

            // Receiving goods is what completes a PO. Sync after the save so the received-quantity
            // query includes this GRN's lines.
            await SyncReferencedPurchaseOrderStatusAsync(purchase.ReferenceStockMain_ID, userId);
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
            .Where(p => p.StockMain_ID == referencePo.StockMainID
                        && p.PaymentType == PaymentType.PAYMENT.ToString()
                        && !p.IsVoided)
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
                    CreatedAt = AppTime.Now,
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
            CreatedAt = AppTime.Now,
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
            // Round to the stored precision BEFORE validating or deriving any money from it, so
            // the arithmetic here and the row the database keeps describe the same transaction.
            detail.Quantity = TransactionAmounts.NormalizeQuantity(detail.Quantity);

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

        ApplyHeaderDiscountToLines(purchase);
    }

    /// <summary>
    /// A header discount changes what the goods actually COST, so it is folded into the lines
    /// pro-rata (remainder on the last line) and the header fields are cleared. Left on the
    /// header, the voucher would debit stock by the line totals but credit the supplier by the
    /// discounted total — an unbalanced voucher — and the authoritative GRN cost would ignore
    /// the discount.
    /// </summary>
    private static void ApplyHeaderDiscountToLines(StockMain purchase)
    {
        if (purchase.DiscountPercent <= 0)
        {
            purchase.DiscountAmount = 0;
            return;
        }

        var subTotal = purchase.StockDetails.Sum(d => d.LineTotal);
        var headerDiscount = Math.Round(subTotal * purchase.DiscountPercent / 100, 2);
        if (headerDiscount > subTotal)
        {
            throw new InvalidOperationException("Discount cannot exceed the goods value.");
        }

        var remaining = headerDiscount;
        var lines = purchase.StockDetails.ToList();
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var share = i == lines.Count - 1 || subTotal == 0
                ? remaining
                : Math.Round(headerDiscount * line.LineTotal / subTotal, 2);

            line.LineTotal = Math.Round(line.LineTotal - share, 2);
            line.LineCost = line.LineTotal;
            line.CostPrice = line.Quantity > 0 ? Math.Round(line.LineTotal / line.Quantity, 2) : line.CostPrice;
            line.UnitPrice = line.CostPrice;
            remaining = Math.Round(remaining - share, 2);
        }

        purchase.DiscountPercent = 0;
        purchase.DiscountAmount = 0;
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
            CreatedAt = AppTime.Now,
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
            CreatedAt = AppTime.Now,
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

        // Money can only leave a cash or bank account. Without this the client can name any
        // ledger account (e.g. a revenue account) and post DR Supplier / CR Revenue —
        // fabricating income while no cash actually moves.
        if (cashBankAccount.AccountType_ID != AccountingConstants.CashAccountTypeId
            && cashBankAccount.AccountType_ID != AccountingConstants.BankAccountTypeId)
            throw new InvalidOperationException("Payment account must be a Cash or Bank account.");

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
            CreatedAt = AppTime.Now,
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
            CreatedAt = AppTime.Now,
            CreatedBy = userId
        };

        await _paymentRepository.AddAsync(payment);

        return voucher;
    }

    private async Task<string> GeneratePaymentReferenceAsync()
    {
        var datePrefix = DocumentNumberSequence.DatePrefix("PAY");
        await DocumentNumberSequence.SerializeAsync(_unitOfWork, datePrefix);

        var lastPayment = await _paymentRepository.Query()
            .Where(p => p.Reference != null && p.Reference.StartsWith(datePrefix))
            .OrderByDescending(p => p.Reference)
            .FirstOrDefaultAsync();

        return DocumentNumberSequence.Next(datePrefix, lastPayment?.Reference);
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
                var unitRate = firstDetail.UnitPrice > 0 ? firstDetail.UnitPrice : firstDetail.CostPrice;
                remainingDetails.Add(new StockDetail
                {
                    Product_ID = firstDetail.Product_ID,
                    Product = firstDetail.Product,
                    Quantity = remainingQty,
                    CostPrice = unitRate,
                    UnitPrice = unitRate,
                    LineTotal = Math.Round(remainingQty * unitRate, 2),
                    LineCost = Math.Round(remainingQty * unitRate, 2)
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

    public async Task<StockMain> UpdateAsync(StockMain purchase, int userId)
    {
        var existing = await _stockMainRepository.Query()
            .Include(s => s.TransactionType)
            .Include(s => s.StockDetails)
            .FirstOrDefaultAsync(s => s.StockMainID == purchase.StockMainID
                                   && s.TransactionType!.Code == TRANSACTION_TYPE_CODE);

        if (existing == null)
            throw new InvalidOperationException("Purchase (GRN) not found.");

        if (existing.Status != "Approved")
            throw new InvalidOperationException("Only approved purchases can be edited.");

        // Optimistic concurrency. A MISSING token is itself a failure, not "no check to run" —
        // treating absence as a pass let any caller opt out of concurrency control merely by
        // leaving the field off, which is exactly what a stripped form post does.
        if (purchase.RowVersion is not { Length: > 0 })
        {
            throw new DbUpdateConcurrencyException(
                "This purchase was submitted without its concurrency token. Reload it and try again.");
        }

        if (!existing.RowVersion.SequenceEqual(purchase.RowVersion))
        {
            throw new DbUpdateConcurrencyException(
                "This purchase was modified by another user after you opened it. Reload and try again.");
        }

        // Block editing if active returns exist
        var hasActiveReturns = await _stockMainRepository.Query()
            .Include(s => s.TransactionType)
            .AnyAsync(s => s.TransactionType!.Code == "PRTN"
                        && s.Status != "Void"
                        && s.ReferenceStockMain_ID == purchase.StockMainID);

        if (hasActiveReturns)
            throw new InvalidOperationException("Cannot edit this purchase — active Purchase Returns reference it. Void the returns first.");

        // Block editing if non-advance payments exist (payments made via PaymentsIndex)
        var hasDirectPayments = await _paymentRepository.Query()
            .AnyAsync(p => p.StockMain_ID == purchase.StockMainID
                        && !p.IsVoided);

        if (hasDirectPayments)
            throw new InvalidOperationException("Cannot edit this purchase — payments have been made against it. Void the payments first.");

        // Credit-note value settles a GRN without leaving a Payment row, so the check above cannot
        // see it. Editing the total below what the credit already settled would strand the excess.
        var hasAppliedCreditNotes = await _allocationRepository.Query()
            .AnyAsync(a => a.StockMain_ID == purchase.StockMainID
                        && a.SourceType == SupplierCreditAllocationSource);

        if (hasAppliedCreditNotes)
            throw new InvalidOperationException("Cannot edit this purchase — supplier credit notes have been applied to it. Void the purchase to release the credit first.");

        // Normalize and validate lines before touching the ledger.
        NormalizePurchaseLines(purchase);

        // A closed financial period must block the edit for BOTH the original date
        // (the ledger we are about to reverse) and the new date (what we re-post to).
        var originalTransactionDate = existing.TransactionDate;
        await ValidatePeriodAsync(originalTransactionDate);
        if (purchase.TransactionDate.Date != originalTransactionDate.Date)
        {
            await ValidatePeriodAsync(purchase.TransactionDate);
        }

        return await ExecuteInTransactionAsync(async () =>
        {
            // An edit that reduces received quantity removes stock that may already have been
            // sold. Check the net reduction per product against current stock-on-hand (locked,
            // so a concurrent sale cannot slip between the check and the save).
            var oldQty = existing.StockDetails
                .GroupBy(d => d.Product_ID)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
            var newQty = purchase.StockDetails
                .GroupBy(d => d.Product_ID)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
            var reductionByProduct = oldQty.Keys.Union(newQty.Keys)
                .Select(pid => (pid, reduction: oldQty.GetValueOrDefault(pid) - newQty.GetValueOrDefault(pid)))
                .Where(x => x.reduction > 0)
                .ToDictionary(x => x.pid, x => x.reduction);
            await EnsureRemovalLeavesNonNegativeStockAsync(_productService, reductionByProduct, "reduce this purchase's received quantity");

            // Update header fields
            existing.Party_ID = purchase.Party_ID;
            existing.TransactionDate = purchase.TransactionDate;
            existing.Remarks = purchase.Remarks;
            existing.UpdatedAt = AppTime.Now;
            existing.UpdatedBy = userId;

            if (existing.ReferenceStockMain_ID.HasValue)
            {
                var po = await _stockMainRepository.Query()
                    .Include(s => s.TransactionType)
                    .Include(s => s.StockDetails)
                    .FirstOrDefaultAsync(s => s.StockMainID == existing.ReferenceStockMain_ID.Value
                                           && s.TransactionType!.Code == PO_TRANSACTION_TYPE_CODE);

                if (po != null)
                {
                    await ValidateGrnAgainstPurchaseOrderAsync(purchase, po);
                }
            }

            // Clear and re-add details
            existing.StockDetails.Clear();
            foreach (var detail in purchase.StockDetails)
            {
                existing.StockDetails.Add(new StockDetail
                {
                    Product_ID = detail.Product_ID,
                    Quantity = detail.Quantity,
                    UnitPrice = detail.UnitPrice,
                    CostPrice = detail.CostPrice,
                    DiscountPercent = detail.DiscountPercent,
                    DiscountAmount = detail.DiscountAmount,
                    LineTotal = detail.LineTotal,
                    LineCost = detail.LineCost,
                    Remarks = detail.Remarks
                });
            }

            // Recalculate totals (preserve existing paid/balance)
            CalculateTotals(existing);
            existing.BalanceAmount = Math.Max(0, existing.TotalAmount - existing.PaidAmount);
            existing.PaymentStatus = CalculatePaymentStatus(existing.PaidAmount, existing.BalanceAmount);

            // Persist the header/detail changes first so derived stock reflects the edit.
            _stockMainRepository.Update(existing);
            await _unitOfWork.SaveChangesAsync();

            // The original Purchase Voucher is now stale (wrong amount/supplier/stock accounts).
            // Reverse it and re-post a fresh voucher so the GL stays in sync with the edited GRN.
            await CreateReversalVouchersForSourceAsync(
                "StockMain", existing.StockMainID, userId, $"GRN {existing.TransactionNo} edited.");

            var repostedVoucher = await CreatePurchaseVoucherAsync(existing, userId);
            existing.Voucher = repostedVoucher;

            _stockMainRepository.Update(existing);
            await _unitOfWork.SaveChangesAsync();

            // Sync AFTER the save: the status depends on a query of received quantity, which must
            // see this edit's new line quantities. Still inside the transaction, so it rolls back
            // with everything else. An edit can reduce quantity (re-open) or complete the PO.
            await SyncReferencedPurchaseOrderStatusAsync(existing.ReferenceStockMain_ID, userId);
            await _unitOfWork.SaveChangesAsync();

            return existing;
        });
    }

    public async Task<bool> VoidAsync(int id, string reason, int userId)
    {
        return await ExecuteInTransactionAsync(async () =>
        {
            var purchase = await _stockMainRepository.Query()
                .Include(s => s.TransactionType)
                .Include(s => s.StockDetails)
                .FirstOrDefaultAsync(s => s.StockMainID == id && s.TransactionType!.Code == TRANSACTION_TYPE_CODE);

            if (purchase == null || purchase.Status == "Void")
                return false;

            await ValidatePeriodAsync(purchase.TransactionDate);

            // Voiding a GRN removes the received stock — verify it is still on hand
            // (goods already sold cannot be un-received without going negative).
            var removalByProduct = purchase.StockDetails
                .GroupBy(d => d.Product_ID)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
            await EnsureRemovalLeavesNonNegativeStockAsync(_productService, removalByProduct, "void this purchase");

            // Block voiding if non-voided Purchase Returns reference this GRN
            var hasActiveReturns = await _stockMainRepository.Query()
                .Include(s => s.TransactionType)
                .AnyAsync(s => s.TransactionType!.Code == "PRTN"
                            && s.Status != "Void"
                            && s.ReferenceStockMain_ID == id);

            if (hasActiveReturns)
                throw new InvalidOperationException("Cannot void this Purchase because it has active Purchase Return(s). Void the return(s) first.");

            purchase.Status = "Void";
            purchase.VoidReason = reason;
            purchase.VoidedAt = AppTime.Now;
            purchase.VoidedBy = userId;

            // Reverse ONLY the purchase (inventory / accounts-payable) voucher. Any money that was
            // actually paid against this GRN is NOT clawed back — instead it is converted into a
            // supplier advance (see below) that can be refunded or auto-adjusted against a future GRN.
            // The Purchase Voucher (PV) is the one linked on StockMain.Voucher_ID; payment vouchers
            // (CP/BP) are linked from their Payment rows and are deliberately left intact.
            if (purchase.Voucher_ID.HasValue)
            {
                await CreateReversalVoucherAsync(purchase.Voucher_ID, userId, reason, "StockMain", purchase.StockMainID);
            }
            else
            {
                // Legacy safety net: if the PV link is missing, reverse by source but keep going.
                await CreateReversalVouchersForSourceAsync("StockMain", purchase.StockMainID, userId, reason);
            }

            // Convert payments made against this GRN into a supplier-level advance, and retire the
            // inert ADJUSTMENT markers (the prior advance they consumed is restored automatically
            // because this GRN's TotalAmount is now excluded from the supplier balance).
            var linkedPayments = await _paymentRepository.Query()
                .Where(p => p.StockMain_ID == purchase.StockMainID && !p.IsVoided)
                .ToListAsync();

            foreach (var payment in linkedPayments)
            {
                if (payment.PaymentType == PaymentType.ADJUSTMENT.ToString())
                {
                    payment.IsVoided = true;
                }
                else
                {
                    // Detach to a supplier-level on-account advance (StockMain_ID = null keeps it
                    // counted in the supplier balance so it is available to refund / adjust later).
                    payment.StockMain_ID = null;
                    payment.Remarks = string.IsNullOrWhiteSpace(payment.Remarks)
                        ? $"Converted to supplier advance on void of {purchase.TransactionNo}"
                        : $"{payment.Remarks} | Converted to supplier advance on void of {purchase.TransactionNo}";
                }

                _paymentRepository.Update(payment);
            }

            // Credit notes applied to this GRN post no voucher and leave no Payment row, so the
            // allocation record is the only trace. Hand the credit back to the note (mirroring how
            // cash payments become supplier advances) instead of letting the void consume it.
            await RestoreSupplierCreditNotesAsync(purchase, userId);

            _stockMainRepository.Update(purchase);
            await _unitOfWork.SaveChangesAsync();

            // Sync AFTER the save so the received-quantity query sees this GRN as Void and stops
            // counting it — a void frees up the PO quantity it had received.
            await SyncReferencedPurchaseOrderStatusAsync(purchase.ReferenceStockMain_ID, userId);
            await _unitOfWork.SaveChangesAsync();

            return true;
        });
    }

    /// <summary>
    /// Sole owner of the referenced Purchase Order's Approved &lt;-&gt; Completed transition, in both
    /// directions, persisted deliberately as part of the GRN operation that caused it:
    /// <list type="bullet">
    /// <item>Approved -> Completed once every ordered line has been fully received.</item>
    /// <item>Completed -> Approved when a GRN is voided or reduced so quantity is outstanding again
    /// (otherwise the PO would vanish from the GRN screen with goods still to receive).</item>
    /// </list>
    /// Must be called from inside the GRN transaction, before its SaveChanges.
    /// </summary>
    private async Task SyncReferencedPurchaseOrderStatusAsync(int? referenceStockMainId, int userId)
    {
        if (!referenceStockMainId.HasValue)
        {
            return;
        }

        var po = await _stockMainRepository.Query()
            .Include(s => s.TransactionType)
            .Include(s => s.StockDetails)
            .FirstOrDefaultAsync(s => s.StockMainID == referenceStockMainId.Value
                                   && s.TransactionType!.Code == PO_TRANSACTION_TYPE_CODE);

        // Draft/Void POs have no auto-status to maintain.
        if (po == null
            || (!string.Equals(po.Status, "Approved", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(po.Status, "Completed", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var receivedLines = await _stockMainRepository.Query()
            .AsNoTracking()
            .Include(s => s.TransactionType)
            .Where(s => s.TransactionType!.Code == TRANSACTION_TYPE_CODE
                     && s.Status != "Void"
                     && s.ReferenceStockMain_ID == po.StockMainID)
            .SelectMany(s => s.StockDetails.Select(d => new { d.Product_ID, d.Quantity }))
            .ToListAsync();

        var receivedByProduct = receivedLines
            .GroupBy(x => x.Product_ID)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

        // Completion is a QUANTITY question, not a money one — deliberately not
        // PurchaseOrderMath.RemainingTotal, which measures the value of the outstanding portion
        // (correct for the supplier advance cap, wrong here: a zero-priced bonus line contributes
        // no value but is still goods that have not arrived).
        var orderedByProduct = (po.StockDetails ?? new List<StockDetail>())
            .GroupBy(d => d.Product_ID)
            .ToDictionary(g => g.Key, g => g.Sum(d => d.Quantity));

        // A PO with no lines is not "fully received" — guard against All() returning true on an
        // empty sequence and silently completing it.
        var fullyReceived = orderedByProduct.Count > 0
            && orderedByProduct.All(o =>
            {
                receivedByProduct.TryGetValue(o.Key, out var receivedQty);
                return receivedQty >= o.Value;
            });

        var desiredStatus = fullyReceived ? "Completed" : "Approved";
        if (string.Equals(po.Status, desiredStatus, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        po.Status = desiredStatus;
        po.UpdatedAt = AppTime.Now;
        po.UpdatedBy = userId;
        _stockMainRepository.Update(po);
    }

    /// <summary>
    /// Returns the supplier's available on-account advance (money we have paid that is not yet
    /// consumed by purchases) — i.e. Max(0, -balance). Used to bound supplier refunds.
    /// </summary>
    public async Task<decimal> GetSupplierAdvanceAsync(int supplierId)
    {
        var balance = await GetSupplierBalanceAsync(supplierId);
        return Math.Max(0, -balance);
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
        var paymentsQuery = _paymentRepository.Query()
            .Include(p => p.StockMain)
            .Where(p => p.Party_ID == supplierId
                        && p.PaymentType == PaymentType.PAYMENT.ToString()
                        && !p.IsVoided
                        && (!p.StockMain_ID.HasValue || p.StockMain == null || p.StockMain.Status != "Void"));

        // When excluding a transaction (e.g. computing the advance available to auto-adjust a GRN
        // being created), also exclude that transaction's OWN payments. Advance transferred onto the
        // GRN is already applied to it via PaidAmount; counting it here too would double-credit it.
        if (excludeTransactionId.HasValue)
        {
            paymentsQuery = paymentsQuery.Where(p => p.StockMain_ID != excludeTransactionId.Value);
        }

        var payments = await paymentsQuery.SumAsync(p => p.Amount);

        // Refunds received back from the supplier (e.g. an advance refunded) undo a prior payment,
        // so they move the balance back up (reduce our advance).
        var refundsQuery = _paymentRepository.Query()
            .Include(p => p.StockMain)
            .Where(p => p.Party_ID == supplierId
                        && p.PaymentType == PaymentType.REFUND.ToString()
                        && !p.IsVoided
                        && (!p.StockMain_ID.HasValue || p.StockMain == null || p.StockMain.Status != "Void"));

        if (excludeTransactionId.HasValue)
        {
            refundsQuery = refundsQuery.Where(p => p.StockMain_ID != excludeTransactionId.Value);
        }

        var refunds = await refundsQuery.SumAsync(p => p.Amount);

        return balance + purchases - returns - payments + refunds;
    }

    /// <summary>
    /// Confirms the counterparty on a goods receipt can actually BE a supplier.
    ///
    /// <para>
    /// A purchase voucher CREDITS this party's ledger account. Booked against a customer, that
    /// credit lands on a receivable — a debit-balance asset — pushing it negative, while every
    /// payables report filters on party type and so never shows the money owed. The sale side has
    /// always run the mirror image of this check (see SaleService.GetPartyForVoucherAsync); the
    /// purchase side resolved the party by id alone and validated nothing.
    /// </para>
    /// </summary>
    private async Task ValidateSupplierAsync(int partyId)
    {
        var supplier = await _partyRepository.Query()
            .AsNoTracking()
            .Include(p => p.Account)
            .FirstOrDefaultAsync(p => p.PartyID == partyId);

        if (supplier == null)
            throw new InvalidOperationException("Selected supplier does not exist.");

        if (!supplier.IsActive)
            throw new InvalidOperationException($"'{supplier.Name}' is deactivated and cannot be transacted with.");

        var isSupplier = supplier.PartyType.Equals("Supplier", StringComparison.OrdinalIgnoreCase)
                      || supplier.PartyType.Equals("Both", StringComparison.OrdinalIgnoreCase);

        if (!isSupplier)
        {
            throw new InvalidOperationException(
                $"'{supplier.Name}' is a {supplier.PartyType.ToLowerInvariant()}, not a supplier. " +
                "A goods receipt credits the supplier's payable account, so it cannot be booked against a customer.");
        }

        if (supplier.Account_ID == null || supplier.Account == null || !supplier.Account.IsActive)
        {
            throw new InvalidOperationException(
                $"'{supplier.Name}' does not have an active linked account. " +
                "Please update the supplier before posting purchase vouchers.");
        }
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

        var productIds = requestedByProduct.Keys.Union(orderedByProduct.Keys).ToList();
        var products = await _productRepository.Query()
            .Where(p => productIds.Contains(p.ProductID))
            .ToDictionaryAsync(p => p.ProductID, p => p.Name);

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
                products.TryGetValue(productId, out var productName);
                productName ??= $"Product ID {productId}";
                throw new InvalidOperationException(
                    $"GRN quantity for ({productName}) exceeds PO quantity. " +
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
