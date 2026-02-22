using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Application.Settings;
using PharmaCare.Domain.Entities.Accounting;
using PharmaCare.Domain.Entities.Configuration;
using PharmaCare.Domain.Entities.Transactions;
using PharmaCare.Application.Interfaces.Configuration;

namespace PharmaCare.Application.Implementations.Transactions;

/// <summary>
/// Service for managing Sales with double-entry accounting.
/// </summary>
public class SaleService : TransactionServiceBase, ISaleService
{
    private readonly IRepository<TransactionType> _transactionTypeRepository;
    private readonly IRepository<VoucherType> _voucherTypeRepository;
    private readonly IRepository<Account> _accountRepository;
    private readonly IRepository<Product> _productRepository;
    private readonly SystemAccountSettings _systemAccounts;
    private readonly IProductService _productService;
    private readonly IPartyService _partyService;

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
        IUnitOfWork unitOfWork,
        IOptions<SystemAccountSettings> systemAccountSettings,
        IProductService productService,
        IPartyService partyService)
        : base(stockMainRepository, voucherRepository, unitOfWork)
    {
        _transactionTypeRepository = transactionTypeRepository;
        _voucherTypeRepository = voucherTypeRepository;
        _accountRepository = accountRepository;
        _productRepository = productRepository;
        _systemAccounts = systemAccountSettings.Value;
        _productService = productService;
        _partyService = partyService;
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

    public async Task<StockMain> CreateAsync(StockMain sale, int userId, int? paymentAccountId = null)
    {
        return await ExecuteInTransactionAsync(async () =>
        {
            // Get the SALE transaction type
            var transactionType = await _transactionTypeRepository.Query()
                .FirstOrDefaultAsync(t => t.Code == TRANSACTION_TYPE_CODE);

            if (transactionType == null)
                throw new InvalidOperationException($"Transaction type '{TRANSACTION_TYPE_CODE}' not found.");

            sale.TransactionType_ID = transactionType.TransactionTypeID;
            sale.TransactionNo = await GenerateTransactionNoAsync(PREFIX);
            sale.Status = "Approved"; // Sales are immediately approved (stock impact)
            sale.CreatedAt = DateTime.Now;
            sale.CreatedBy = userId;

            // Calculate totals
            CalculateTotals(sale);

            // Validate Sale (Stock & Walking Customer)
            await ValidateSaleAsync(sale);

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

                await CreatePaymentVoucherAsync(sale, paymentAccountId.Value, userId);
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
            var walkingCustomerAccount = await _accountRepository.Query()
                .FirstOrDefaultAsync(a => a.AccountID == _systemAccounts.WalkingCustomerAccountId);

            if (walkingCustomerAccount == null)
                throw new InvalidOperationException($"Walking Customer account (ID: {_systemAccounts.WalkingCustomerAccountId}) not found.");

            customerAccount = walkingCustomerAccount;
            customerName = "Walk-in Customer";
        }

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

        foreach (var item in productDetailsMap)
        {
            var category = item.Product!.Category!;
            var lineTotal = item.Detail.LineTotal;
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

        // Get customer account
        Account customerAccount;
        string customerName;

        if (sale.Party_ID.HasValue && sale.Party?.Account != null)
        {
            customerAccount = sale.Party.Account;
            customerName = sale.Party.Name;
        }
        else
        {
            var walkingCustomerAccount = await _accountRepository.Query()
                .FirstOrDefaultAsync(a => a.AccountID == _systemAccounts.WalkingCustomerAccountId);

            if (walkingCustomerAccount == null)
                throw new InvalidOperationException($"Walking Customer account not found.");

            customerAccount = walkingCustomerAccount;
            customerName = "Walk-in Customer";
        }

        // Get payment account to determine voucher prefix (CR for Cash, BR for Bank)
        var paymentAccount = await _accountRepository.GetByIdAsync(paymentAccountId);
        if (paymentAccount == null)
            throw new InvalidOperationException($"Payment account ID {paymentAccountId} not found.");

        string voucherPrefix = "RV";
        if (paymentAccount.AccountType_ID == 1) // Cash
        {
            voucherPrefix = "CR";
        }
        else if (paymentAccount.AccountType_ID == 2) // Bank
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

    public async Task<bool> VoidAsync(int id, string reason, int userId)
    {
        return await ExecuteInTransactionAsync(async () =>
        {
            var sale = await _stockMainRepository.Query()
                .Include(s => s.TransactionType)
                .FirstOrDefaultAsync(s => s.StockMainID == id && s.TransactionType!.Code == TRANSACTION_TYPE_CODE);

            if (sale == null || sale.Status == "Void")
                return false;

            sale.Status = "Void";
            sale.VoidReason = reason;
            sale.VoidedAt = DateTime.Now;
            sale.VoidedBy = userId;

            // Reverse all posted vouchers linked to this StockMain (invoice + payments, if any)
            await CreateReversalVouchersForSourceAsync("StockMain", sale.StockMainID, userId, reason);

            _stockMainRepository.Update(sale);
            await _unitOfWork.SaveChangesAsync();

            return true;
        });
    }

    private async Task ValidateSaleAsync(StockMain sale)
    {
        // 1. Stock Validation
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

        // 2. Walking Customer Validation
        bool isWalkingCustomer = false;
        
        // Check 1: Explicit System Account ID Match (Reliable)
        if (sale.Party_ID.HasValue)
        {
            var party = await _partyService.GetByIdWithAccountAsync(sale.Party_ID.Value);
            
            // Log for debugging
            // Console.WriteLine($"Validating Sale - PartyID: {party?.PartyID}, Name: {party?.Name}, AccountID: {party?.Account?.AccountID}");

            if (party != null)
            {
                if (party.Account != null && party.Account.AccountID == _systemAccounts.WalkingCustomerAccountId)
                {
                    isWalkingCustomer = true;
                }
                
                // Check 2: Name Fallback
                if (!isWalkingCustomer)
                {
                    var name = party.Name.ToLower();
                    if (name.Contains("walkin") || name.Contains("walk-in") || name.Contains("walk in") || name.Contains("counter"))
                    {
                        isWalkingCustomer = true;
                    }
                }
            }
        }
        else
        {
            // If logic allows null Party_ID to mean Walking Customer
            isWalkingCustomer = true; 
        }

        if (isWalkingCustomer)
        {
            // Paid Amount must equal Total Amount
            // Allow small buffer for floating point
            if (sale.PaidAmount < (sale.TotalAmount - 0.01m))
            {
                 throw new InvalidOperationException(
                    "Walking/Counter Customers must pay the full amount immediately. " +
                    $"Total: {sale.TotalAmount}, Paid: {sale.PaidAmount}");
            }
        }
    }
}
