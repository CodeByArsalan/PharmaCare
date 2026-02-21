using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Application.Settings;
using PharmaCare.Domain.Entities.Accounting;
using PharmaCare.Domain.Entities.Configuration;
using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.Application.Implementations.Transactions;

/// <summary>
/// Service for managing Sale Returns.
/// </summary>
public class SaleReturnService : TransactionServiceBase, ISaleReturnService
{
    private readonly IRepository<TransactionType> _transactionTypeRepository;
    private readonly IRepository<VoucherType> _voucherTypeRepository;
    private readonly IRepository<Product> _productRepository;
    private readonly IRepository<Account> _accountRepository;
    private readonly IRepository<Party> _partyRepository;
    private readonly SystemAccountSettings _systemAccounts;

    private const string TRANSACTION_TYPE_CODE = "SRTN";
    private const string SALE_TRANSACTION_TYPE_CODE = "SALE";
    private const string PREFIX = "SRTN";
    private const string SALE_RETURN_VOUCHER_CODE = "SV"; // We typically use Sales Voucher type but effectively reverse it, OR a specific SRV type. 

    public SaleReturnService(
        IRepository<StockMain> stockMainRepository,
        IRepository<TransactionType> transactionTypeRepository,
        IRepository<Voucher> voucherRepository,
        IRepository<VoucherType> voucherTypeRepository,
        IRepository<Product> productRepository,
        IRepository<Account> accountRepository,
        IRepository<Party> partyRepository,
        IUnitOfWork unitOfWork,
        IOptions<SystemAccountSettings> systemAccountSettings)
        : base(stockMainRepository, voucherRepository, unitOfWork)
    {
        _transactionTypeRepository = transactionTypeRepository;
        _voucherTypeRepository = voucherTypeRepository;
        _productRepository = productRepository;
        _accountRepository = accountRepository;
        _partyRepository = partyRepository;
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

    public async Task<StockMain> CreateAsync(StockMain saleReturn, int userId)
    {
        // 1. Validate Return against Original Sale
        if (!saleReturn.ReferenceStockMain_ID.HasValue)
        {
            throw new InvalidOperationException("Sale Return must reference an original Sale.");
        }

        var originalSale = await _stockMainRepository.Query()
            .Include(s => s.StockDetails)
            .FirstOrDefaultAsync(s => s.StockMainID == saleReturn.ReferenceStockMain_ID.Value);

        if (originalSale == null)
        {
            throw new InvalidOperationException("Original Sale not found.");
        }

        // Server-side quantity validation against the reference sale
        await ValidateReturnQuantitiesAsync(saleReturn);
        await PopulateMissingLineCostsFromReferenceSaleAsync(saleReturn);

        // 2. Prepare StockMain
        var transactionType = await _transactionTypeRepository.Query()
            .FirstOrDefaultAsync(t => t.Code == TRANSACTION_TYPE_CODE);

        if (transactionType == null)
            throw new InvalidOperationException($"Transaction type '{TRANSACTION_TYPE_CODE}' not found.");

        saleReturn.TransactionType_ID = transactionType.TransactionTypeID;
        saleReturn.TransactionNo = await GenerateTransactionNoAsync(PREFIX);
        saleReturn.Status = "Approved";
        saleReturn.CreatedAt = DateTime.Now;
        saleReturn.CreatedBy = userId;
        saleReturn.Party_ID = originalSale.Party_ID; // Same party as original sale

        // Calculate totals
        CalculateTotals(saleReturn);

        // 3. Save StockMain
        await _stockMainRepository.AddAsync(saleReturn);
        await _unitOfWork.SaveChangesAsync();

        // 4. Create Accounting Voucher
        var voucher = await CreateSaleReturnVoucherAsync(saleReturn, userId);
        saleReturn.Voucher_ID = voucher.VoucherID;

        // 5. Update Sale Return with Voucher ID
        _stockMainRepository.Update(saleReturn);
        
        // Update the reference Sale's balance if linked
        if (saleReturn.ReferenceStockMain_ID.HasValue)
        {
            var sale = await _stockMainRepository.Query()
                .Include(s => s.TransactionType)
                .FirstOrDefaultAsync(s => s.StockMainID == saleReturn.ReferenceStockMain_ID.Value
                                       && s.TransactionType!.Code == SALE_TRANSACTION_TYPE_CODE);

            if (sale != null)
            {
                await RecalculateSaleBalanceIncludingReturnsAsync(sale, userId);
                _stockMainRepository.Update(sale);
            }
        }

        await _unitOfWork.SaveChangesAsync();

        return saleReturn;
    }

    private async Task<Voucher> CreateSaleReturnVoucherAsync(StockMain saleReturn, int userId)
    {
        // Sale Return Voucher typically reverses the Sale logic
        // Debit: Sales Return Account (or Sales Account directly)
        // Debit: Stock Account (Inventory comes back)
        // Credit: COGS Account (Cost is reversed)
        // Credit: Customer Account (Receivable decreases)

        var voucherType = await _voucherTypeRepository.Query()
            .FirstOrDefaultAsync(vt => vt.Code == SALE_RETURN_VOUCHER_CODE || vt.Code == "JV");

        if (voucherType == null)
             throw new InvalidOperationException($"Voucher type '{SALE_RETURN_VOUCHER_CODE}' or 'JV' not found.");

        // Get customer account
        Account customerAccount;
        string customerName;

        if (saleReturn.Party_ID.HasValue)
        {
            var party = saleReturn.Party;
            // Need to load party if not loaded, but usually it is attached or we need to query
            // Since we set Party_ID from Original Sale, let's look it up or trust navigation if present
            if (party == null)
            {
                 // Use _partyRepository instead of _accountRepository
                 var loadedParty = await _partyRepository.Query()
                     .Include(p => p.Account)
                     .FirstOrDefaultAsync(p => p.PartyID == saleReturn.Party_ID);

                 if (loadedParty != null)
                 {
                     if (loadedParty.Account != null)
                     {
                         customerAccount = loadedParty.Account;
                         customerName = loadedParty.Name;
                     }
                     else
                     {
                         throw new InvalidOperationException($"Party '{loadedParty.Name}' does not have an account.");
                     }
                 }
                 else
                 {
                     if (saleReturn.Party_ID == null)
                     {
                         var walkingCustomerAccount = await _accountRepository.Query()
                            .FirstOrDefaultAsync(a => a.AccountID == _systemAccounts.WalkingCustomerAccountId);
                         
                         customerAccount = walkingCustomerAccount!;
                         customerName = "Walk-in Customer";
                     }
                     else
                     {
                         throw new InvalidOperationException("Party not found and no account linked for Sale Return.");
                     }
                 }
            }
            else
            {
                 if (party.Account != null)
                 {
                     customerAccount = party.Account;
                     customerName = party.Name;
                 }
                 else 
                 {
                      throw new InvalidOperationException("Party does not have an account.");
                 }
            }
        }
        else
        {
            // Walk-in
            var walkingCustomerAccount = await _accountRepository.Query()
                .FirstOrDefaultAsync(a => a.AccountID == _systemAccounts.WalkingCustomerAccountId);
            
            customerAccount = walkingCustomerAccount!;
            customerName = "Walk-in Customer";
        }

        // Generate Voucher No
        // Use SRV- instead of SV? Or just SV?
        // Let's use SRV- for Returns to distinguish
        var voucherNo = await GenerateVoucherNoAsync("SRV");

        var voucherDetails = new List<VoucherDetail>();

        // Load products
        var productIds = saleReturn.StockDetails.Select(d => d.Product_ID).Distinct().ToList();
        var products = await _productRepository.Query()
            .Include(p => p.Category)
            .Where(p => productIds.Contains(p.ProductID))
            .ToListAsync();

        // 1. Credit: Customer Account (Reduces Receivable) - Total Amount
        voucherDetails.Add(new VoucherDetail
        {
            Account_ID = customerAccount.AccountID,
            DebitAmount = 0,
            CreditAmount = saleReturn.TotalAmount,
            Description = $"Sale Return from {customerName}",
            Party_ID = saleReturn.Party_ID
        });

        // Loop details to aggregate Sales Return, Stock, COGS
        var salesReturnByAccount = new Dictionary<int, decimal>(); // Debit
        var stockByAccount = new Dictionary<int, decimal>();       // Debit (Asset Increase)
        var cogsByAccount = new Dictionary<int, decimal>();        // Credit (Expense Decrease)

        foreach (var detail in saleReturn.StockDetails)
        {
            var product = products.FirstOrDefault(p => p.ProductID == detail.Product_ID);
            var category = product?.Category;

            if (category == null) continue;

            // Sales Account (for Return)
            // Use SaleReturnAccount_ID if exists, otherwise SaleAccount_ID
            // Actually usually we Debit Sales Account directly to reduce Revenue
            int salesAccountId = category.SaleAccount_ID ?? 0;
            if (salesAccountId != 0)
            {
                if (salesReturnByAccount.ContainsKey(salesAccountId))
                    salesReturnByAccount[salesAccountId] += detail.LineTotal;
                else
                    salesReturnByAccount[salesAccountId] = detail.LineTotal;
            }

            // Stock & COGS
            if (detail.LineCost > 0)
            {
                int stockAccountId = category.StockAccount_ID ?? 0;
                int cogsAccountId = category.COGSAccount_ID ?? 0;

                if (stockAccountId != 0)
                {
                    if (stockByAccount.ContainsKey(stockAccountId))
                        stockByAccount[stockAccountId] += detail.LineCost;
                    else
                        stockByAccount[stockAccountId] = detail.LineCost;
                }

                if (cogsAccountId != 0)
                {
                    if (cogsByAccount.ContainsKey(cogsAccountId))
                        cogsByAccount[cogsAccountId] += detail.LineCost;
                    else
                        cogsByAccount[cogsAccountId] = detail.LineCost;
                }
            }
        }

        // 2. Debit: Sales Accounts
        foreach (var (accId, amt) in salesReturnByAccount)
        {
            voucherDetails.Add(new VoucherDetail
            {
                Account_ID = accId,
                DebitAmount = amt,
                CreditAmount = 0,
                Description = "Sales Return"
            });
        }

        // 3. Debit: Stock Accounts
        foreach (var (accId, amt) in stockByAccount)
        {
            voucherDetails.Add(new VoucherDetail
            {
                Account_ID = accId,
                DebitAmount = amt,
                CreditAmount = 0,
                Description = "Inventory Restock (Return)"
            });
        }

        // 4. Credit: COGS Accounts
        foreach (var (accId, amt) in cogsByAccount)
        {
            voucherDetails.Add(new VoucherDetail
            {
                Account_ID = accId,
                DebitAmount = 0,
                CreditAmount = amt,
                Description = "COGS Reversal (Return)"
            });
        }
        
        // Totals
        // Dr: SalesReturn + Stock
        // Cr: Customer + COGS
        var totalLinesDebit = salesReturnByAccount.Values.Sum() + stockByAccount.Values.Sum();
        var totalLinesCredit = saleReturn.TotalAmount + cogsByAccount.Values.Sum();

        var voucher = new Voucher
        {
            VoucherType_ID = voucherType.VoucherTypeID,
            VoucherNo = voucherNo,
            VoucherDate = saleReturn.TransactionDate,
            TotalDebit = totalLinesDebit,
            TotalCredit = totalLinesCredit,
            Status = "Posted",
            SourceTable = "StockMain",
            SourceID = saleReturn.StockMainID,
            Narration = $"Sale Return from {customerName}. Ref: {saleReturn.TransactionNo}",
            CreatedAt = DateTime.Now,
            CreatedBy = userId,
            VoucherDetails = voucherDetails
        };

        await _voucherRepository.AddAsync(voucher);
        await _unitOfWork.SaveChangesAsync();

        return voucher;
    }

    /// <summary>
    /// Validates return quantities against original sale quantities minus non-voided previous returns.
    /// </summary>
    private async Task ValidateReturnQuantitiesAsync(StockMain saleReturn)
    {
        if (!saleReturn.ReferenceStockMain_ID.HasValue)
            return;

        var sale = await _stockMainRepository.Query()
            .Include(s => s.TransactionType)
            .Include(s => s.StockDetails)
            .FirstOrDefaultAsync(s => s.StockMainID == saleReturn.ReferenceStockMain_ID.Value
                                   && s.TransactionType!.Code == SALE_TRANSACTION_TYPE_CODE
                                   && s.Status == "Approved");

        if (sale == null)
            throw new InvalidOperationException("Reference Sale not found or not approved.");

        if (saleReturn.Party_ID.HasValue && sale.Party_ID.HasValue && saleReturn.Party_ID.Value != sale.Party_ID.Value)
            throw new InvalidOperationException("Selected customer does not match the reference sale customer.");

        var existingReturns = await _stockMainRepository.Query()
            .Include(s => s.TransactionType)
            .Include(s => s.StockDetails)
            .Where(s => s.TransactionType!.Code == TRANSACTION_TYPE_CODE
                     && s.ReferenceStockMain_ID == sale.StockMainID
                     && s.Status != "Void")
            .ToListAsync();

        var alreadyReturned = existingReturns
            .SelectMany(r => r.StockDetails)
            .GroupBy(d => d.Product_ID)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

        var requested = saleReturn.StockDetails
            .GroupBy(d => d.Product_ID)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

        foreach (var (productId, requestedQty) in requested)
        {
            var saleDetail = sale.StockDetails.FirstOrDefault(d => d.Product_ID == productId);
            if (saleDetail == null)
                throw new InvalidOperationException($"Product ID {productId} was not found in the reference sale.");

            var previouslyReturned = alreadyReturned.TryGetValue(productId, out var qty) ? qty : 0;
            var availableForReturn = saleDetail.Quantity - previouslyReturned;

            if (requestedQty > availableForReturn)
            {
                throw new InvalidOperationException(
                    $"Return quantity ({requestedQty}) for product ID {productId} exceeds available quantity ({availableForReturn}). " +
                    $"Sale Qty: {saleDetail.Quantity}, Already Returned: {previouslyReturned}.");
            }
        }
    }

    /// <summary>
    /// If cost fields are missing on return lines, derive weighted average unit cost from reference sale lines.
    /// </summary>
    private async Task PopulateMissingLineCostsFromReferenceSaleAsync(StockMain saleReturn)
    {
        if (!saleReturn.ReferenceStockMain_ID.HasValue)
            return;

        var sale = await _stockMainRepository.Query()
            .Include(s => s.TransactionType)
            .Include(s => s.StockDetails)
            .FirstOrDefaultAsync(s => s.StockMainID == saleReturn.ReferenceStockMain_ID.Value
                                   && s.TransactionType!.Code == SALE_TRANSACTION_TYPE_CODE);

        if (sale == null)
            return;

        var sourceCostByProduct = sale.StockDetails
            .GroupBy(d => d.Product_ID)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    Quantity = g.Sum(x => x.Quantity),
                    LineCost = g.Sum(x => x.LineCost)
                });

        foreach (var detail in saleReturn.StockDetails)
        {
            if (detail.CostPrice > 0 && detail.LineCost > 0)
                continue;

            if (!sourceCostByProduct.TryGetValue(detail.Product_ID, out var source) || source.Quantity <= 0)
                continue;

            var unitCost = Math.Round(source.LineCost / source.Quantity, 4);

            if (detail.CostPrice <= 0)
                detail.CostPrice = unitCost;

            detail.LineCost = Math.Round(detail.Quantity * detail.CostPrice, 2);
        }
    }

    private async Task RecalculateSaleBalanceIncludingReturnsAsync(StockMain sale, int userId, int? excludeReturnId = null)
    {
        var returnQuery = _stockMainRepository.Query()
            .Include(s => s.TransactionType)
            .Where(s => s.TransactionType!.Code == TRANSACTION_TYPE_CODE
                     && s.ReferenceStockMain_ID == sale.StockMainID
                     && s.Status != "Void");

        if (excludeReturnId.HasValue)
        {
            returnQuery = returnQuery.Where(s => s.StockMainID != excludeReturnId.Value);
        }

        var totalReturns = await returnQuery.SumAsync(s => (decimal?)s.TotalAmount) ?? 0;

        sale.BalanceAmount = sale.TotalAmount - sale.PaidAmount - totalReturns;
        sale.PaymentStatus = sale.BalanceAmount <= 0
            ? "Paid"
            : (sale.PaidAmount > 0 ? "Partial" : "Unpaid");
        sale.UpdatedAt = DateTime.Now;
        sale.UpdatedBy = userId;
    }

    public async Task<IEnumerable<StockMain>> GetSalesForReturnAsync(int? customerId = null)
    {
        var query = _stockMainRepository.Query()
            .Include(s => s.TransactionType)
            .Include(s => s.Party)
            .Include(s => s.StockDetails)
                .ThenInclude(d => d.Product)
            .Where(s => s.TransactionType!.Code == SALE_TRANSACTION_TYPE_CODE 
                     && s.Status == "Approved");

        if (customerId.HasValue)
        {
            query = query.Where(s => s.Party_ID == customerId.Value);
        }

        return await query
            .OrderByDescending(s => s.TransactionDate)
            .ToListAsync();
    }

    public async Task<bool> VoidAsync(int id, string reason, int userId)
    {
        var saleReturn = await GetByIdAsync(id);
        if (saleReturn == null)
            return false;

        if (saleReturn.Status == "Void")
            return false;

        saleReturn.Status = "Void";
        saleReturn.VoidReason = reason;
        saleReturn.VoidedAt = DateTime.Now;
        saleReturn.VoidedBy = userId;

        // Create reversal voucher if original voucher exists
        if (saleReturn.Voucher_ID.HasValue)
        {
            await CreateReversalVoucherAsync(saleReturn.Voucher_ID.Value, userId, reason, "StockMain", saleReturn.StockMainID);
        }
        
        // Restore the reference Sale's balance if linked
        if (saleReturn.ReferenceStockMain_ID.HasValue)
        {
            var sale = await _stockMainRepository.Query()
                .Include(s => s.TransactionType)
                .FirstOrDefaultAsync(s => s.StockMainID == saleReturn.ReferenceStockMain_ID.Value
                                       && s.TransactionType!.Code == SALE_TRANSACTION_TYPE_CODE);

            if (sale != null)
            {
                await RecalculateSaleBalanceIncludingReturnsAsync(sale, userId);
                _stockMainRepository.Update(sale);
            }
        }

        _stockMainRepository.Update(saleReturn);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }
}
