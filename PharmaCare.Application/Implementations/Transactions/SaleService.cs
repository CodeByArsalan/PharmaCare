using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Domain.Entities.Accounting;
using PharmaCare.Domain.Entities.Configuration;
using PharmaCare.Domain.Entities.Finance;
using PharmaCare.Domain.Entities.Transactions;
using PharmaCare.Domain.Enums;
using PaymentStatusEnum = PharmaCare.Domain.Enums.PaymentStatus;

namespace PharmaCare.Application.Implementations.Transactions;

/// <summary>
/// Service for managing Sales with double-entry accounting.
/// </summary>
public class SaleService : TransactionServiceBase, ISaleService
{
    private const int CashAccountTypeId = 1;
    private const int BankAccountTypeId = 2;

    private readonly IRepository<TransactionType> _transactionTypeRepository;
    private readonly IRepository<VoucherType> _voucherTypeRepository;
    private readonly IRepository<Account> _accountRepository;
    private readonly IRepository<Product> _productRepository;
    private readonly IRepository<Payment> _paymentRepository;
    private readonly IRepository<PaymentAllocation> _paymentAllocationRepository;
    private readonly IRepository<Party> _partyRepository;
    private readonly IProductService _productService;

    private const string TRANSACTION_TYPE_CODE = "SALE";
    private const string PREFIX = "SALE";
    private const string VOUCHER_TYPE_CODE = "SV"; // Sales Voucher
    private const string PAYMENT_VOUCHER_TYPE_CODE = "RV"; // Receipt Voucher

    public SaleService(
        IRepository<StockMain> stockMainRepository,
        IRepository<TransactionType> transactionTypeRepository,
        IRepository<Voucher> voucherRepository,
        IRepository<VoucherType> voucherTypeRepository,
        IRepository<Account> accountRepository,
        IRepository<Product> productRepository,
        IRepository<Payment> paymentRepository,
        IRepository<PaymentAllocation> paymentAllocationRepository,
        IRepository<Party> partyRepository,
        IUnitOfWork unitOfWork,
        IProductService productService)
        : base(stockMainRepository, voucherRepository, unitOfWork)
    {
        _transactionTypeRepository = transactionTypeRepository;
        _voucherTypeRepository = voucherTypeRepository;
        _accountRepository = accountRepository;
        _productRepository = productRepository;
        _paymentRepository = paymentRepository;
        _paymentAllocationRepository = paymentAllocationRepository;
        _partyRepository = partyRepository;
        _productService = productService;
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

    public async Task<(decimal OutstandingBalance, int OpenInvoices)> GetCustomerOutstandingSummaryAsync(int customerId)
    {
        if (customerId <= 0)
        {
            return (0m, 0);
        }

        var sales = await _stockMainRepository.Query()
            .Include(s => s.TransactionType)
            .Where(s =>
                s.TransactionType!.Code == TRANSACTION_TYPE_CODE &&
                s.Party_ID == customerId &&
                !string.Equals(s.Status, TransactionStatus.Void.ToString(), StringComparison.OrdinalIgnoreCase))
            .Select(s => new
            {
                s.StockMainID,
                s.TotalAmount,
                s.PaidAmount
            })
            .ToListAsync();

        if (sales.Count == 0)
        {
            return (0m, 0);
        }

        var saleIds = sales.Select(s => s.StockMainID).ToList();
        var totalReturnsBySale = await _stockMainRepository.Query()
            .Include(s => s.TransactionType)
            .Where(s =>
                s.TransactionType!.Code == "SRTN" &&
                s.ReferenceStockMain_ID.HasValue &&
                saleIds.Contains(s.ReferenceStockMain_ID.Value) &&
                !string.Equals(s.Status, TransactionStatus.Void.ToString(), StringComparison.OrdinalIgnoreCase))
            .GroupBy(s => s.ReferenceStockMain_ID!.Value)
            .Select(g => new
            {
                SaleId = g.Key,
                TotalReturns = g.Sum(x => x.TotalAmount)
            })
            .ToDictionaryAsync(x => x.SaleId, x => x.TotalReturns);

        decimal outstandingBalance = 0;
        var openInvoices = 0;

        foreach (var sale in sales)
        {
            var totalReturns = totalReturnsBySale.TryGetValue(sale.StockMainID, out var returnedAmount) ? returnedAmount : 0m;
            var balance = Math.Max(0m, sale.TotalAmount - sale.PaidAmount - totalReturns);
            if (balance > 0)
            {
                outstandingBalance += balance;
                openInvoices++;
            }
        }

        return (Math.Round(outstandingBalance, 2), openInvoices);
    }

    public async Task<StockMain> CreateAsync(StockMain sale, int userId, int? paymentAccountId = null)
    {
        return await ExecuteInTransactionAsync(async () =>
        {
            NormalizeSaleLines(sale);

            // Get the SALE transaction type
            var transactionType = await _transactionTypeRepository.Query()
                .FirstOrDefaultAsync(t => t.Code == TRANSACTION_TYPE_CODE);

            if (transactionType == null)
                throw new InvalidOperationException($"Transaction type '{TRANSACTION_TYPE_CODE}' not found.");

            sale.TransactionType_ID = transactionType.TransactionTypeID;
            sale.TransactionNo = await GenerateTransactionNoAsync(PREFIX);
            sale.Status = TransactionStatus.Approved.ToString(); // Sales are immediately approved (stock impact)
            sale.CreatedAt = DateTime.Now;
            sale.CreatedBy = userId;

            // Calculate totals
            CalculateTotals(sale);

            if (sale.PaidAmount < 0)
            {
                throw new InvalidOperationException("Paid amount cannot be negative.");
            }

            // Treat incoming PaidAmount as tendered amount and cap posting at invoice total.
            sale.PaidAmount = Math.Min(sale.TotalAmount, sale.PaidAmount);
            sale.BalanceAmount = Math.Max(0, sale.TotalAmount - sale.PaidAmount);

            // Validate Sale (stock availability)
            await ValidateSaleAsync(sale);

            // Set payment status based on paid amount
            if (sale.PaidAmount >= sale.TotalAmount)
            {
                sale.PaymentStatus = PaymentStatusEnum.Paid.ToString();
            }
            else if (sale.PaidAmount > 0)
            {
                sale.PaymentStatus = PaymentStatusEnum.Partial.ToString();
            }
            else
            {
                sale.PaymentStatus = PaymentStatusEnum.Unpaid.ToString();
            }

            // Save StockMain and StockDetails first to generate StockMainID for SourceID links.
            await _stockMainRepository.AddAsync(sale);
            await _unitOfWork.SaveChangesAsync();

            var saleVoucher = await CreateSaleVoucherAsync(sale, userId);
            sale.Voucher = saleVoucher;

            if (sale.PaidAmount > 0)
            {
                if (!paymentAccountId.HasValue || paymentAccountId <= 0)
                {
                    throw new InvalidOperationException(
                        "Payment Account is required when making a payment. " +
                        "Please select a Cash or Bank account from the dropdown.");
                }

                var paymentVoucher = await CreatePaymentVoucherAsync(sale, paymentAccountId.Value, userId);
                await CreateInitialPaymentRecordAsync(sale, paymentVoucher, paymentAccountId.Value, userId);
            }

            _stockMainRepository.Update(sale);
            await _unitOfWork.SaveChangesAsync();

            return sale;
        });
    }

    /// <summary>
    /// Creates a sales voucher with double-entry accounting using category-specific accounts.
    /// Sale Voucher:
    ///   Debit: Customer Account (AR) - what they owe us
    ///   Credit: Sales Account (per category) - income
    ///   Debit: COGS Account (per category) - cost of goods sold
    ///   Credit: Stock Account (per category) - inventory reduction
    /// </summary>
    private async Task<Voucher> CreateSaleVoucherAsync(StockMain sale, int userId)
    {
        // Get Sales Voucher type
        var voucherType = await _voucherTypeRepository.Query()
            .FirstOrDefaultAsync(vt => vt.Code == VOUCHER_TYPE_CODE || vt.Code == "JV");

        if (voucherType == null)
            throw new InvalidOperationException($"Voucher type '{VOUCHER_TYPE_CODE}' or 'JV' not found.");

        var customerParty = await ResolveCustomerPartyWithAccountAsync(sale);
        var customerAccount = customerParty.Account!;
        var customerName = customerParty.Name;

        // Load products with categories for each detail line
        var productIds = sale.StockDetails.Select(d => d.Product_ID).Distinct().ToList();
        var products = await _productRepository.Query()
            .Include(p => p.Category)
            .Where(p => productIds.Contains(p.ProductID))
            .ToListAsync();

        var voucherNo = await GenerateVoucherNoAsync(VOUCHER_TYPE_CODE);
        var voucherDetails = new List<VoucherDetail>();

        // 1. Debit: Customer Account (total sale amount)
        voucherDetails.Add(new VoucherDetail
        {
            Account_ID = customerAccount.AccountID,
            DebitAmount = sale.TotalAmount,
            CreditAmount = 0,
            Description = $"Sale to {customerName}",
            Party_ID = sale.Party_ID
        });

        // Validate all products have categories
        var productDetailsMap = sale.StockDetails
            .Select(d => new
            {
                Detail = d,
                Product = products.FirstOrDefault(p => p.ProductID == d.Product_ID)
            })
            .ToList();

        var productsWithoutCategory = productDetailsMap
            .Where(x => x.Product?.Category == null)
            .Select(x => x.Product?.Name ?? $"ProductID: {x.Detail.Product_ID}")
            .ToList();

        if (productsWithoutCategory.Any())
        {
            throw new InvalidOperationException(
                $"The following products do not have a category assigned: {string.Join(", ", productsWithoutCategory)}. " +
                "Please assign categories to all products before creating a sale.");
        }

        // Build aggregated entries by account ID
        // Dictionary to aggregate amounts by account ID
        var salesByAccount = new Dictionary<int, decimal>();   // Sales Account -> Credit Amount
        var cogsByAccount = new Dictionary<int, decimal>();    // COGS Account -> Debit Amount
        var stockByAccount = new Dictionary<int, decimal>();   // Stock Account -> Credit Amount

        decimal totalCOGS = 0;
        var netLineAmounts = AllocateNetSalesByLine(sale);
        var detailIndex = 0;

        foreach (var item in productDetailsMap)
        {
            var category = item.Product!.Category!;
            var lineTotal = netLineAmounts[detailIndex++];
            var lineCost = item.Detail.LineCost;

            // Validate category has Sales Account configured
            if (!category.SaleAccount_ID.HasValue)
            {
                throw new InvalidOperationException(
                    $"Category '{category.Name}' does not have a Sales Account configured. " +
                    "Please configure the Sales Account in Category settings.");
            }

            // Aggregate Sales by account
            if (salesByAccount.ContainsKey(category.SaleAccount_ID.Value))
                salesByAccount[category.SaleAccount_ID.Value] += lineTotal;
            else
                salesByAccount[category.SaleAccount_ID.Value] = lineTotal;

            // Only process COGS/Stock if there's actual cost
            if (lineCost > 0)
            {
                // Validate category has COGS and Stock Accounts configured
                if (!category.COGSAccount_ID.HasValue)
                {
                    throw new InvalidOperationException(
                        $"Category '{category.Name}' does not have a COGS Account configured. " +
                        "Please configure the COGS Account in Category settings.");
                }
                if (!category.StockAccount_ID.HasValue)
                {
                    throw new InvalidOperationException(
                        $"Category '{category.Name}' does not have a Stock Account configured. " +
                        "Please configure the Stock Account in Category settings.");
                }

                // Aggregate COGS by account
                if (cogsByAccount.ContainsKey(category.COGSAccount_ID.Value))
                    cogsByAccount[category.COGSAccount_ID.Value] += lineCost;
                else
                    cogsByAccount[category.COGSAccount_ID.Value] = lineCost;

                // Aggregate Stock by account
                if (stockByAccount.ContainsKey(category.StockAccount_ID.Value))
                    stockByAccount[category.StockAccount_ID.Value] += lineCost;
                else
                    stockByAccount[category.StockAccount_ID.Value] = lineCost;

                totalCOGS += lineCost;
            }
        }

        // 2. Credit: Sales Account(s) - aggregated
        foreach (var (accountId, amount) in salesByAccount)
        {
            voucherDetails.Add(new VoucherDetail
            {
                Account_ID = accountId,
                DebitAmount = 0,
                CreditAmount = amount,
                Description = "Sales Revenue"
            });
        }

        // 3. Debit: COGS Account(s) - aggregated
        foreach (var (accountId, amount) in cogsByAccount)
        {
            voucherDetails.Add(new VoucherDetail
            {
                Account_ID = accountId,
                DebitAmount = amount,
                CreditAmount = 0,
                Description = "Cost of Goods Sold"
            });
        }

        // 4. Credit: Stock Account(s) - aggregated (reduces inventory)
        foreach (var (accountId, amount) in stockByAccount)
        {
            voucherDetails.Add(new VoucherDetail
            {
                Account_ID = accountId,
                DebitAmount = 0,
                CreditAmount = amount,
                Description = "Inventory Reduction"
            });
        }

        // Calculate voucher totals (Customer DR + COGS DR = Sales CR + Stock CR)
        var totalDebit = sale.TotalAmount + totalCOGS;
        var totalCredit = sale.TotalAmount + totalCOGS;

        var voucher = new Voucher
        {
            VoucherType_ID = voucherType.VoucherTypeID,
            VoucherNo = voucherNo,
            VoucherDate = sale.TransactionDate,
            TotalDebit = totalDebit,
            TotalCredit = totalCredit,
            Status = "Posted",
            SourceTable = "StockMain",
            SourceID = sale.StockMainID, // Now available since StockMain is saved first
            Narration = $"Sale to {customerName}. Invoice: {sale.TransactionNo}",
            CreatedAt = DateTime.Now,
            CreatedBy = userId,
            VoucherDetails = voucherDetails
        };

        await _voucherRepository.AddAsync(voucher);

        return voucher;
    }

    /// <summary>
    /// Creates a payment voucher (Receipt Voucher) for sales with payment.
    ///   Debit: Cash/Bank Account - money received
    ///   Credit: Customer Account - reduces receivable
    /// </summary>
    private async Task<Voucher> CreatePaymentVoucherAsync(StockMain sale, int paymentAccountId, int userId)
    {
        var voucherType = await _voucherTypeRepository.Query()
            .FirstOrDefaultAsync(vt => vt.Code == PAYMENT_VOUCHER_TYPE_CODE || vt.Code == "JV");

        if (voucherType == null)
            throw new InvalidOperationException($"Voucher type '{PAYMENT_VOUCHER_TYPE_CODE}' or 'JV' not found.");

        var customerParty = await ResolveCustomerPartyWithAccountAsync(sale);
        var customerAccount = customerParty.Account!;
        var customerName = customerParty.Name;

        // Get payment account to determine voucher prefix (CR for Cash, BR for Bank)
        var paymentAccount = await _accountRepository.GetByIdAsync(paymentAccountId);
        if (paymentAccount == null)
            throw new InvalidOperationException($"Payment account ID {paymentAccountId} not found.");

        if (paymentAccount.AccountType_ID != CashAccountTypeId && paymentAccount.AccountType_ID != BankAccountTypeId)
        {
            throw new InvalidOperationException("Payment account must be a Cash or Bank account.");
        }

        string voucherPrefix = "RV";
        if (paymentAccount.AccountType_ID == CashAccountTypeId) // Cash
        {
            voucherPrefix = "CR";
        }
        else if (paymentAccount.AccountType_ID == BankAccountTypeId) // Bank
        {
            voucherPrefix = "BR";
        }

        // Use base class method but we need to pass custom prefix
        var voucherNo = await GenerateVoucherNoAsync(voucherPrefix);

        var voucherDetails = new List<VoucherDetail>
        {
            // Debit: Cash/Bank Account
            new VoucherDetail
            {
                Account_ID = paymentAccountId,
                DebitAmount = sale.PaidAmount,
                CreditAmount = 0,
                Description = $"Payment received for {sale.TransactionNo}"
            },
            // Credit: Customer Account
            new VoucherDetail
            {
                Account_ID = customerAccount.AccountID,
                DebitAmount = 0,
                CreditAmount = sale.PaidAmount,
                Description = $"Payment from {customerName}",
                Party_ID = sale.Party_ID
            }
        };

        var voucher = new Voucher
        {
            VoucherType_ID = voucherType.VoucherTypeID,
            VoucherNo = voucherNo,
            VoucherDate = sale.TransactionDate,
            TotalDebit = sale.PaidAmount,
            TotalCredit = sale.PaidAmount,
            Status = "Posted",
            SourceTable = "StockMain",
            SourceID = sale.StockMainID, // Now available since StockMain is saved first
            Narration = $"Payment received from {customerName} for {sale.TransactionNo}",
            CreatedAt = DateTime.Now,
            CreatedBy = userId,
            VoucherDetails = voucherDetails
        };

        await _voucherRepository.AddAsync(voucher);

        return voucher;
    }

    private async Task CreateInitialPaymentRecordAsync(StockMain sale, Voucher paymentVoucher, int paymentAccountId, int userId)
    {
        if (!sale.Party_ID.HasValue || sale.Party_ID.Value <= 0)
        {
            throw new InvalidOperationException(
                "Customer is required for sale payment posting. Please select a customer before saving.");
        }

        var partyId = sale.Party_ID.Value;

        var paymentAccount = await _accountRepository.GetByIdAsync(paymentAccountId);
        if (paymentAccount == null)
        {
            throw new InvalidOperationException($"Payment account ID {paymentAccountId} not found.");
        }

        if (paymentAccount.AccountType_ID != CashAccountTypeId && paymentAccount.AccountType_ID != BankAccountTypeId)
        {
            throw new InvalidOperationException("Payment account must be a Cash or Bank account.");
        }

        var paymentMethod = paymentAccount.AccountType_ID == BankAccountTypeId
            ? PaymentMethod.Bank.ToString()
            : PaymentMethod.Cash.ToString();

        var payment = new Payment
        {
            PaymentType = PaymentType.RECEIPT.ToString(),
            Party_ID = partyId,
            StockMain_ID = sale.StockMainID,
            Account_ID = paymentAccountId,
            Amount = sale.PaidAmount,
            PaymentDate = sale.TransactionDate,
            PaymentMethod = paymentMethod,
            Reference = $"REC-{sale.TransactionNo}",
            Remarks = $"Initial payment captured during sale {sale.TransactionNo}",
            Voucher = paymentVoucher,
            CreatedAt = DateTime.Now,
            CreatedBy = userId
        };

        await _paymentRepository.AddAsync(payment);

        await _paymentAllocationRepository.AddAsync(new PaymentAllocation
        {
            Payment = payment,
            StockMain_ID = sale.StockMainID,
            Amount = sale.PaidAmount,
            SourceType = "Receipt",
            AllocationDate = sale.TransactionDate,
            Remarks = $"Initial payment captured during sale {sale.TransactionNo}",
            CreatedAt = DateTime.Now,
            CreatedBy = userId
        });
    }

    private async Task<Party> ResolveCustomerPartyWithAccountAsync(StockMain sale)
    {
        if (!sale.Party_ID.HasValue || sale.Party_ID.Value <= 0)
        {
            throw new InvalidOperationException(
                "Customer is required for sales posting. Please select a customer.");
        }

        if (sale.Party != null && sale.Party.Account != null && sale.Party.IsActive && sale.Party.Account.IsActive)
        {
            return sale.Party;
        }

        var party = await _partyRepository.Query()
            .Include(p => p.Account)
            .Where(p =>
                p.IsActive &&
                p.Account_ID.HasValue &&
                p.PartyID == sale.Party_ID.Value &&
                (p.PartyType.ToLower() == "customer" || p.PartyType.ToLower() == "both"))
            .FirstOrDefaultAsync();

        if (party?.Account == null || !party.Account.IsActive)
        {
            throw new InvalidOperationException(
                "Selected customer does not have an active linked account. " +
                "Please update the customer party before posting sale vouchers.");
        }

        return party;
    }

    public async Task<bool> VoidAsync(int id, string reason, int userId)
    {
        return await ExecuteInTransactionAsync(async () =>
        {
            var sale = await _stockMainRepository.Query()
                .Include(s => s.TransactionType)
                .FirstOrDefaultAsync(s => s.StockMainID == id && s.TransactionType!.Code == TRANSACTION_TYPE_CODE);

            if (sale == null || sale.Status == TransactionStatus.Void.ToString())
                return false;

            sale.Status = TransactionStatus.Void.ToString();
            sale.VoidReason = reason;
            sale.VoidedAt = DateTime.Now;
            sale.VoidedBy = userId;

            // Reverse all posted vouchers linked to this StockMain (invoice + payments, if any)
            await CreateReversalVouchersForSourceAsync("StockMain", sale.StockMainID, userId, reason);
            await CreateReversalVouchersForLinkedReceiptsAsync(sale.StockMainID, userId, reason);
            await VoidLinkedReceiptsAsync(sale.StockMainID, userId, reason);

            _stockMainRepository.Update(sale);
            await _unitOfWork.SaveChangesAsync();

            return true;
        });
    }

    private async Task ValidateSaleAsync(StockMain sale)
    {
        // Customer and account are required for voucher posting
        sale.Party = await ResolveCustomerPartyWithAccountAsync(sale);

        // Stock Validation
        var productIds = sale.StockDetails.Select(d => d.Product_ID).Distinct().ToList();
        var stockStatus = await _productService.GetStockStatusAsync(productIds);

        foreach (var detail in sale.StockDetails)
        {
            if (stockStatus.TryGetValue(detail.Product_ID, out var currentStock))
            {
                if (detail.Quantity > currentStock)
                {
                    var product = await _productRepository.GetByIdAsync(detail.Product_ID);
                    throw new InvalidOperationException(
                        $"Insufficient stock for product '{product?.Name ?? "ID " + detail.Product_ID}'. " +
                        $"Requested: {detail.Quantity}, Available: {currentStock}");
                }
            }
        }
    }

    private static void NormalizeSaleLines(StockMain sale)
    {
        if (sale.StockDetails == null || sale.StockDetails.Count == 0)
        {
            throw new InvalidOperationException("At least one sale line is required.");
        }

        foreach (var detail in sale.StockDetails)
        {
            if (detail.Product_ID <= 0)
            {
                throw new InvalidOperationException("Each line must have a valid product.");
            }

            if (detail.Quantity <= 0)
            {
                throw new InvalidOperationException("Line quantity must be greater than zero.");
            }

            if (detail.UnitPrice < 0 || detail.CostPrice < 0)
            {
                throw new InvalidOperationException("Unit and cost prices cannot be negative.");
            }

            var grossAmount = Math.Round(detail.Quantity * detail.UnitPrice, 2);
            var lineDiscount = detail.DiscountPercent > 0
                ? Math.Round(grossAmount * detail.DiscountPercent / 100, 2)
                : Math.Round(Math.Max(0, detail.DiscountAmount), 2);

            if (lineDiscount > grossAmount)
            {
                lineDiscount = grossAmount;
            }

            detail.DiscountAmount = lineDiscount;
            detail.LineTotal = Math.Round(grossAmount - lineDiscount, 2);
            detail.LineCost = Math.Round(detail.Quantity * detail.CostPrice, 2);
        }
    }

    private static List<decimal> AllocateNetSalesByLine(StockMain sale)
    {
        var allocations = new List<decimal>();
        if (sale.StockDetails == null || sale.StockDetails.Count == 0)
        {
            return allocations;
        }

        var details = sale.StockDetails.ToList();

        var grossSubTotal = details.Sum(d => d.LineTotal);
        if (grossSubTotal <= 0 || sale.TotalAmount <= 0)
        {
            allocations.AddRange(Enumerable.Repeat(0m, details.Count));
            return allocations;
        }

        var remaining = sale.TotalAmount;
        for (var i = 0; i < details.Count; i++)
        {
            decimal netAmount;
            if (i == details.Count - 1)
            {
                netAmount = Math.Round(remaining, 2);
            }
            else
            {
                netAmount = Math.Round((details[i].LineTotal / grossSubTotal) * sale.TotalAmount, 2);
                remaining -= netAmount;
            }

            allocations.Add(Math.Max(0, netAmount));
        }

        return allocations;
    }

    private async Task CreateReversalVouchersForLinkedReceiptsAsync(int saleId, int userId, string reason)
    {
        var receiptVoucherIds = await _paymentRepository.Query()
            .Where(p =>
                p.StockMain_ID == saleId &&
                p.PaymentType == PaymentType.RECEIPT.ToString() &&
                p.Voucher_ID.HasValue)
            .Select(p => p.Voucher_ID!.Value)
            .Distinct()
            .ToListAsync();

        foreach (var voucherId in receiptVoucherIds)
        {
            await CreateReversalVoucherAsync(voucherId, userId, reason, "StockMain", saleId);
        }
    }

    private async Task VoidLinkedReceiptsAsync(int saleId, int userId, string reason)
    {
        var linkedReceipts = await _paymentRepository.Query()
            .Where(p =>
                p.StockMain_ID == saleId &&
                p.PaymentType == PaymentType.RECEIPT.ToString() &&
                !p.IsVoided)
            .ToListAsync();

        if (linkedReceipts.Count == 0)
        {
            return;
        }

        var now = DateTime.Now;
        foreach (var receipt in linkedReceipts)
        {
            receipt.IsVoided = true;
            receipt.VoidReason = $"Sale voided: {reason}";
            receipt.VoidedAt = now;
            receipt.VoidedBy = userId;
            receipt.UpdatedAt = now;
            receipt.UpdatedBy = userId;
            _paymentRepository.Update(receipt);
        }

        var receiptIds = linkedReceipts.Select(r => r.PaymentID).ToList();
        var allocations = await _paymentAllocationRepository.Query()
            .Where(a => a.Payment_ID.HasValue && receiptIds.Contains(a.Payment_ID.Value))
            .ToListAsync();

        if (allocations.Count > 0)
        {
            _paymentAllocationRepository.RemoveRange(allocations);
        }
    }
}
