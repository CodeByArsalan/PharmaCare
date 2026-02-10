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
public class SaleReturnService : ISaleReturnService
{
    private readonly IRepository<StockMain> _stockMainRepository;
    private readonly IRepository<TransactionType> _transactionTypeRepository;
    private readonly IRepository<Voucher> _voucherRepository;
    private readonly IRepository<VoucherType> _voucherTypeRepository;
    private readonly IRepository<Account> _accountRepository;
    private readonly IRepository<Product> _productRepository;
    private readonly IRepository<Party> _partyRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly SystemAccountSettings _systemAccounts;

    private const string TRANSACTION_TYPE_CODE = "SRTN";
    private const string SALE_TRANSACTION_TYPE_CODE = "SALE";
    private const string PREFIX = "SRTN";
    private const string SALE_RETURN_VOUCHER_CODE = "SRT";

    public SaleReturnService(
        IRepository<StockMain> stockMainRepository,
        IRepository<TransactionType> transactionTypeRepository,
        IRepository<Voucher> voucherRepository,
        IRepository<VoucherType> voucherTypeRepository,
        IRepository<Account> accountRepository,
        IRepository<Product> productRepository,
        IRepository<Party> partyRepository,
        IUnitOfWork unitOfWork,
        IOptions<SystemAccountSettings> systemAccountSettings)
    {
        _stockMainRepository = stockMainRepository;
        _transactionTypeRepository = transactionTypeRepository;
        _voucherRepository = voucherRepository;
        _voucherTypeRepository = voucherTypeRepository;
        _accountRepository = accountRepository;
        _productRepository = productRepository;
        _partyRepository = partyRepository;
        _unitOfWork = unitOfWork;
        _systemAccounts = systemAccountSettings.Value;
    }

    public async Task<IEnumerable<StockMain>> GetAllAsync()
    {
        return await _stockMainRepository.Query()
            .Include(s => s.TransactionType)
            .Include(s => s.Party)
            .Include(s => s.ReferenceStockMain)
            .Include(s => s.Voucher)
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
            .Include(s => s.Voucher)
                .ThenInclude(v => v!.VoucherDetails)
            .Include(s => s.StockDetails)
                .ThenInclude(d => d.Product)
            .FirstOrDefaultAsync(s => s.StockMainID == id && s.TransactionType!.Code == TRANSACTION_TYPE_CODE);
    }

    public async Task<StockMain> CreateAsync(StockMain saleReturn, int userId)
    {
        // Get the SRTN transaction type
        var transactionType = await _transactionTypeRepository.Query()
            .FirstOrDefaultAsync(t => t.Code == TRANSACTION_TYPE_CODE);

        if (transactionType == null)
            throw new InvalidOperationException($"Transaction type '{TRANSACTION_TYPE_CODE}' not found.");

        saleReturn.TransactionType_ID = transactionType.TransactionTypeID;
        saleReturn.TransactionNo = await GenerateTransactionNoAsync();
        saleReturn.Status = "Approved"; // Returns are immediately approved (stock impact)
        saleReturn.PaymentStatus = "Unpaid";
        saleReturn.CreatedAt = DateTime.Now;
        saleReturn.CreatedBy = userId;

        // Calculate totals
        CalculateTotals(saleReturn);

        // Server-side quantity validation against the reference sale
        if (saleReturn.ReferenceStockMain_ID.HasValue)
        {
            await ValidateReturnQuantitiesAsync(saleReturn);
            await PopulateMissingLineCostsFromReferenceSaleAsync(saleReturn);
        }

        await _stockMainRepository.AddAsync(saleReturn);
        await _unitOfWork.SaveChangesAsync();

        // Create accounting voucher for the sale return
        var voucher = await CreateSaleReturnVoucherAsync(saleReturn, userId);
        saleReturn.Voucher_ID = voucher.VoucherID;
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
        var saleReturn = await _stockMainRepository.Query()
            .Include(s => s.TransactionType)
            .Include(s => s.Voucher)
                .ThenInclude(v => v!.VoucherDetails)
            .FirstOrDefaultAsync(s => s.StockMainID == id && s.TransactionType!.Code == TRANSACTION_TYPE_CODE);

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
            await CreateReversalVoucherAsync(saleReturn, userId);
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
                await RecalculateSaleBalanceIncludingReturnsAsync(sale, userId, saleReturn.StockMainID);
                _stockMainRepository.Update(sale);
            }
        }

        _stockMainRepository.Update(saleReturn);
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

    /// <summary>
    /// Creates a Sale Return Voucher (SRT) with double-entry accounting.
    /// Debit: Sales Account(s) - reverses revenue
    /// Credit: Customer Account (AR) - reduces receivable
    /// Debit: Stock Account(s) - restores inventory asset
    /// Credit: COGS Account(s) - reverses cost of goods sold
    /// </summary>
    private async Task<Voucher> CreateSaleReturnVoucherAsync(StockMain saleReturn, int userId)
    {
        var voucherType = await _voucherTypeRepository.Query()
            .FirstOrDefaultAsync(vt => vt.Code == SALE_RETURN_VOUCHER_CODE || vt.Code == "JV");

        if (voucherType == null)
            throw new InvalidOperationException($"Voucher type '{SALE_RETURN_VOUCHER_CODE}' or 'JV' not found.");

        Account customerAccount;
        string customerName;

        if (saleReturn.Party_ID.HasValue && saleReturn.Party_ID > 0)
        {
            var customer = await _partyRepository.Query()
                .Include(p => p.Account)
                .FirstOrDefaultAsync(p => p.PartyID == saleReturn.Party_ID.Value);

            if (customer?.Account_ID == null || customer.Account == null)
                throw new InvalidOperationException("Customer does not have an associated account for accounting entries.");

            customerAccount = customer.Account;
            customerName = customer.Name;
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

        var productIds = saleReturn.StockDetails.Select(d => d.Product_ID).Distinct().ToList();
        var products = await _productRepository.Query()
            .Include(p => p.Category)
            .Where(p => productIds.Contains(p.ProductID))
            .ToListAsync();

        var salesByAccount = new Dictionary<int, decimal>(); // Debit
        var stockByAccount = new Dictionary<int, decimal>(); // Debit
        var cogsByAccount = new Dictionary<int, decimal>();  // Credit
        decimal totalCost = 0;

        foreach (var detail in saleReturn.StockDetails)
        {
            var product = products.FirstOrDefault(p => p.ProductID == detail.Product_ID);
            var category = product?.Category;

            if (category == null)
                throw new InvalidOperationException($"Product '{product?.Name ?? $"ID:{detail.Product_ID}"}' does not have a category assigned.");

            if (!category.SaleAccount_ID.HasValue)
                throw new InvalidOperationException($"Category '{category.Name}' does not have a Sales Account configured.");

            AddAggregate(salesByAccount, category.SaleAccount_ID.Value, detail.LineTotal);

            var lineCost = detail.LineCost;
            if (lineCost <= 0 && detail.CostPrice > 0)
            {
                lineCost = Math.Round(detail.Quantity * detail.CostPrice, 2);
            }

            if (lineCost <= 0)
                continue;

            if (!category.StockAccount_ID.HasValue)
                throw new InvalidOperationException($"Category '{category.Name}' does not have a Stock Account configured.");
            if (!category.COGSAccount_ID.HasValue)
                throw new InvalidOperationException($"Category '{category.Name}' does not have a COGS Account configured.");

            AddAggregate(stockByAccount, category.StockAccount_ID.Value, lineCost);
            AddAggregate(cogsByAccount, category.COGSAccount_ID.Value, lineCost);
            totalCost += lineCost;
        }

        var voucherNo = await GenerateVoucherNoAsync(SALE_RETURN_VOUCHER_CODE);
        var voucherDetails = new List<VoucherDetail>();

        // 1) Debit Sales Account(s) to reverse revenue
        foreach (var (accountId, amount) in salesByAccount)
        {
            voucherDetails.Add(new VoucherDetail
            {
                Account_ID = accountId,
                DebitAmount = amount,
                CreditAmount = 0,
                Description = "Sales Return"
            });
        }

        // 2) Credit Customer Account (AR) to reduce receivable
        voucherDetails.Add(new VoucherDetail
        {
            Account_ID = customerAccount.AccountID,
            DebitAmount = 0,
            CreditAmount = saleReturn.TotalAmount,
            Description = $"Sale return from {customerName}",
            Party_ID = saleReturn.Party_ID
        });

        // 3) Debit Stock Account(s) to restore inventory value
        foreach (var (accountId, amount) in stockByAccount)
        {
            voucherDetails.Add(new VoucherDetail
            {
                Account_ID = accountId,
                DebitAmount = amount,
                CreditAmount = 0,
                Description = "Inventory restored (sale return)"
            });
        }

        // 4) Credit COGS Account(s) to reverse expense
        foreach (var (accountId, amount) in cogsByAccount)
        {
            voucherDetails.Add(new VoucherDetail
            {
                Account_ID = accountId,
                DebitAmount = 0,
                CreditAmount = amount,
                Description = "COGS reversal (sale return)"
            });
        }

        var voucher = new Voucher
        {
            VoucherType_ID = voucherType.VoucherTypeID,
            VoucherNo = voucherNo,
            VoucherDate = saleReturn.TransactionDate,
            TotalDebit = saleReturn.TotalAmount + totalCost,
            TotalCredit = saleReturn.TotalAmount + totalCost,
            Status = "Posted",
            SourceTable = "StockMain",
            SourceID = saleReturn.StockMainID,
            Narration = $"Sale return from {customerName}. Return: {saleReturn.TransactionNo}",
            CreatedAt = DateTime.Now,
            CreatedBy = userId,
            VoucherDetails = voucherDetails
        };

        await _voucherRepository.AddAsync(voucher);
        await _unitOfWork.SaveChangesAsync();

        return voucher;
    }

    /// <summary>
    /// Creates a reversal voucher for a voided sale return by swapping debit/credit amounts.
    /// </summary>
    private async Task CreateReversalVoucherAsync(StockMain saleReturn, int userId)
    {
        var originalVoucher = await _voucherRepository.Query()
            .Include(v => v.VoucherDetails)
            .FirstOrDefaultAsync(v => v.VoucherID == saleReturn.Voucher_ID);

        if (originalVoucher == null || originalVoucher.IsReversed)
            return;

        var voucherNo = await GenerateVoucherNoAsync(SALE_RETURN_VOUCHER_CODE);

        var reversalVoucher = new Voucher
        {
            VoucherType_ID = originalVoucher.VoucherType_ID,
            VoucherNo = voucherNo,
            VoucherDate = DateTime.Now,
            TotalDebit = originalVoucher.TotalDebit,
            TotalCredit = originalVoucher.TotalCredit,
            Status = "Posted",
            SourceTable = "StockMain",
            SourceID = saleReturn.StockMainID,
            Narration = $"Reversal of {originalVoucher.VoucherNo} - Void: {saleReturn.VoidReason}",
            ReversesVoucher_ID = originalVoucher.VoucherID,
            CreatedAt = DateTime.Now,
            CreatedBy = userId
        };

        foreach (var detail in originalVoucher.VoucherDetails)
        {
            reversalVoucher.VoucherDetails.Add(new VoucherDetail
            {
                Account_ID = detail.Account_ID,
                DebitAmount = detail.CreditAmount,
                CreditAmount = detail.DebitAmount,
                Description = $"Reversal - {detail.Description}",
                Party_ID = detail.Party_ID,
                Product_ID = detail.Product_ID
            });
        }

        await _voucherRepository.AddAsync(reversalVoucher);
        await _unitOfWork.SaveChangesAsync();

        originalVoucher.IsReversed = true;
        originalVoucher.ReversedByVoucher_ID = reversalVoucher.VoucherID;
        _voucherRepository.Update(originalVoucher);
        await _unitOfWork.SaveChangesAsync();
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

    private static void AddAggregate(IDictionary<int, decimal> bucket, int accountId, decimal amount)
    {
        if (amount <= 0)
            return;

        if (bucket.ContainsKey(accountId))
            bucket[accountId] += amount;
        else
            bucket[accountId] = amount;
    }

    private void CalculateTotals(StockMain saleReturn)
    {
        saleReturn.SubTotal = saleReturn.StockDetails.Sum(d => d.LineTotal);

        if (saleReturn.DiscountPercent > 0)
        {
            saleReturn.DiscountAmount = Math.Round(saleReturn.SubTotal * saleReturn.DiscountPercent / 100, 2);
        }

        saleReturn.TotalAmount = saleReturn.SubTotal - saleReturn.DiscountAmount;
        saleReturn.BalanceAmount = saleReturn.TotalAmount - saleReturn.PaidAmount;
    }
}
