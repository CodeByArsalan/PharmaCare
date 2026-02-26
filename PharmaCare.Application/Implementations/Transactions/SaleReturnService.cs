using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Domain.Entities.Accounting;
using PharmaCare.Domain.Entities.Configuration;
using PharmaCare.Domain.Entities.Finance;
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
    private readonly IRepository<Party> _partyRepository;
    private readonly IRepository<CreditNote> _creditNoteRepository;

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
        IRepository<Party> partyRepository,
        IRepository<CreditNote> creditNoteRepository,
        IUnitOfWork unitOfWork)
        : base(stockMainRepository, voucherRepository, unitOfWork)
    {
        _transactionTypeRepository = transactionTypeRepository;
        _voucherTypeRepository = voucherTypeRepository;
        _productRepository = productRepository;
        _partyRepository = partyRepository;
        _creditNoteRepository = creditNoteRepository;
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
            .Include(s => s.Voucher)
            .FirstOrDefaultAsync(s => s.StockMainID == id && s.TransactionType!.Code == TRANSACTION_TYPE_CODE);
    }

    public async Task<StockMain> CreateAsync(StockMain saleReturn, int userId)
    {
        return await ExecuteInTransactionAsync(async () =>
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

            if (!originalSale.Party_ID.HasValue || originalSale.Party_ID.Value <= 0)
            {
                throw new InvalidOperationException(
                    "Sale Return requires an original sale linked to a customer. " +
                    "Please update the original sale with a customer and try again.");
            }

            // Server-side quantity validation against the reference sale
            await ValidateReturnQuantitiesAsync(saleReturn);
            await PopulateMissingLineCostsFromReferenceSaleAsync(saleReturn);
            NormalizeReturnLines(saleReturn);

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
                    await CreateCreditNoteForOverpaymentAsync(saleReturn, sale, userId);
                }
            }

            await _unitOfWork.SaveChangesAsync();

            return saleReturn;
        });
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

        var customerParty = await ResolveCustomerPartyWithAccountAsync(saleReturn.Party_ID);
        var customerAccount = customerParty.Account!;
        var customerName = customerParty.Name;

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
        var netLineAmounts = AllocateNetReturnByLine(saleReturn);
        var detailIndex = 0;

        foreach (var detail in saleReturn.StockDetails)
        {
            var product = products.FirstOrDefault(p => p.ProductID == detail.Product_ID);
            var category = product?.Category;

            if (category == null)
            {
                throw new InvalidOperationException($"Product '{product?.Name ?? detail.Product_ID.ToString()}' does not have a category assigned.");
            }

            // Sales Account (for Return)
            // Use SaleReturnAccount_ID if exists, otherwise SaleAccount_ID
            // Actually usually we Debit Sales Account directly to reduce Revenue
            int salesAccountId = category.SaleAccount_ID ?? 0;
            if (salesAccountId == 0)
            {
                throw new InvalidOperationException($"Category '{category.Name}' does not have a Sales Account configured.");
            }
            
            var lineTotal = netLineAmounts[detailIndex++];
            if (salesReturnByAccount.ContainsKey(salesAccountId))
                salesReturnByAccount[salesAccountId] += lineTotal;
            else
                salesReturnByAccount[salesAccountId] = lineTotal;

            // Stock & COGS
            if (detail.LineCost > 0)
            {
                int stockAccountId = category.StockAccount_ID ?? 0;
                int cogsAccountId = category.COGSAccount_ID ?? 0;

                if (stockAccountId == 0)
                {
                    throw new InvalidOperationException($"Category '{category.Name}' does not have a Stock Account configured.");
                }

                if (cogsAccountId == 0)
                {
                    throw new InvalidOperationException($"Category '{category.Name}' does not have a COGS Account configured.");
                }

                if (stockByAccount.ContainsKey(stockAccountId))
                    stockByAccount[stockAccountId] += detail.LineCost;
                else
                    stockByAccount[stockAccountId] = detail.LineCost;

                if (cogsByAccount.ContainsKey(cogsAccountId))
                    cogsByAccount[cogsAccountId] += detail.LineCost;
                else
                    cogsByAccount[cogsAccountId] = detail.LineCost;
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

        return voucher;
    }

    private async Task<Party> ResolveCustomerPartyWithAccountAsync(int? partyId)
    {
        if (!partyId.HasValue || partyId.Value <= 0)
        {
            throw new InvalidOperationException(
                "Customer is required for sale return posting. Please select a customer.");
        }

        var party = await _partyRepository.Query()
            .Include(p => p.Account)
            .Where(p =>
                p.IsActive &&
                p.Account_ID.HasValue &&
                p.PartyID == partyId.Value &&
                (p.PartyType.ToLower() == "customer" || p.PartyType.ToLower() == "both"))
            .FirstOrDefaultAsync();

        if (party?.Account == null || !party.Account.IsActive)
        {
            throw new InvalidOperationException(
                "Selected customer does not have an active linked account. " +
                "Please update the customer party before posting sale return vouchers.");
        }

        return party;
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

    private static void NormalizeReturnLines(StockMain saleReturn)
    {
        if (saleReturn.StockDetails == null || saleReturn.StockDetails.Count == 0)
        {
            throw new InvalidOperationException("At least one return line is required.");
        }

        foreach (var detail in saleReturn.StockDetails)
        {
            if (detail.Product_ID <= 0)
            {
                throw new InvalidOperationException("Each return line must have a valid product.");
            }

            if (detail.Quantity <= 0)
            {
                throw new InvalidOperationException("Return quantity must be greater than zero.");
            }

            if (detail.UnitPrice < 0 || detail.CostPrice < 0)
            {
                throw new InvalidOperationException("Unit and cost prices cannot be negative.");
            }

            detail.DiscountPercent = 0;
            detail.DiscountAmount = 0;
            detail.LineTotal = Math.Round(detail.Quantity * detail.UnitPrice, 2);
            detail.LineCost = Math.Round(detail.Quantity * detail.CostPrice, 2);
        }
    }

    private static List<decimal> AllocateNetReturnByLine(StockMain saleReturn)
    {
        var allocations = new List<decimal>();
        if (saleReturn.StockDetails == null || saleReturn.StockDetails.Count == 0)
        {
            return allocations;
        }

        var details = saleReturn.StockDetails.ToList();

        var grossSubTotal = details.Sum(d => d.LineTotal);
        if (grossSubTotal <= 0 || saleReturn.TotalAmount <= 0)
        {
            allocations.AddRange(Enumerable.Repeat(0m, details.Count));
            return allocations;
        }

        var remaining = saleReturn.TotalAmount;
        for (var i = 0; i < details.Count; i++)
        {
            decimal netAmount;
            if (i == details.Count - 1)
            {
                netAmount = Math.Round(remaining, 2);
            }
            else
            {
                netAmount = Math.Round((details[i].LineTotal / grossSubTotal) * saleReturn.TotalAmount, 2);
                remaining -= netAmount;
            }

            allocations.Add(Math.Max(0, netAmount));
        }

        return allocations;
    }

    private async Task CreateCreditNoteForOverpaymentAsync(StockMain saleReturn, StockMain sale, int userId)
    {
        if (!sale.Party_ID.HasValue || sale.Party_ID.Value <= 0)
        {
            return;
        }

        var totalReturnsBefore = await _stockMainRepository.Query()
            .Include(s => s.TransactionType)
            .Where(s => s.TransactionType!.Code == TRANSACTION_TYPE_CODE
                     && s.ReferenceStockMain_ID == sale.StockMainID
                     && s.StockMainID != saleReturn.StockMainID
                     && s.Status != "Void")
            .SumAsync(s => (decimal?)s.TotalAmount) ?? 0;

        var overpaymentBefore = Math.Max(0, sale.PaidAmount + totalReturnsBefore - sale.TotalAmount);
        var overpaymentAfter = Math.Max(0, sale.PaidAmount + totalReturnsBefore + saleReturn.TotalAmount - sale.TotalAmount);
        var creditAmount = Math.Round(overpaymentAfter - overpaymentBefore, 2);

        if (creditAmount <= 0)
        {
            return;
        }

        var creditNote = new CreditNote
        {
            CreditNoteNo = await GenerateCreditNoteNoAsync(),
            Party_ID = sale.Party_ID.Value,
            SourceStockMain_ID = saleReturn.StockMainID,
            TotalAmount = creditAmount,
            AppliedAmount = 0,
            BalanceAmount = creditAmount,
            CreditDate = saleReturn.TransactionDate,
            Status = "Open",
            Remarks = $"Credit from sale return {saleReturn.TransactionNo}",
            CreatedAt = DateTime.Now,
            CreatedBy = userId
        };

        await _creditNoteRepository.AddAsync(creditNote);
    }

    private async Task<string> GenerateCreditNoteNoAsync()
    {
        var datePrefix = $"CN-{DateTime.Now:yyyyMMdd}-";
        var lastCreditNote = await _creditNoteRepository.Query()
            .Where(c => c.CreditNoteNo.StartsWith(datePrefix))
            .OrderByDescending(c => c.CreditNoteNo)
            .FirstOrDefaultAsync();

        var nextNum = 1;
        if (lastCreditNote?.CreditNoteNo != null)
        {
            var parts = lastCreditNote.CreditNoteNo.Split('-');
            if (parts.Length > 2 && int.TryParse(parts.Last(), out var lastNum))
            {
                nextNum = lastNum + 1;
            }
        }

        return $"{datePrefix}{nextNum:D4}";
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

        var outstanding = sale.TotalAmount - sale.PaidAmount - totalReturns;
        sale.BalanceAmount = Math.Max(0, outstanding);
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
        return await ExecuteInTransactionAsync(async () =>
        {
            var saleReturn = await GetByIdAsync(id);
            if (saleReturn == null || saleReturn.Status == "Void")
            {
                return false;
            }

            var creditNotes = await _creditNoteRepository.Query()
                .Where(c => c.SourceStockMain_ID == saleReturn.StockMainID && c.Status != "Void")
                .ToListAsync();

            foreach (var creditNote in creditNotes)
            {
                if (creditNote.AppliedAmount > 0)
                {
                    throw new InvalidOperationException(
                        $"Credit note '{creditNote.CreditNoteNo}' is already applied. Unapply allocations before voiding this return.");
                }

                creditNote.Status = "Void";
                creditNote.VoidReason = $"Sale Return voided: {reason}";
                creditNote.VoidedAt = DateTime.Now;
                creditNote.VoidedBy = userId;
                creditNote.UpdatedAt = DateTime.Now;
                creditNote.UpdatedBy = userId;

                if (creditNote.Voucher_ID.HasValue)
                {
                    await CreateReversalVoucherAsync(creditNote.Voucher_ID.Value, userId, reason, "StockMain", saleReturn.StockMainID);
                }

                _creditNoteRepository.Update(creditNote);
            }

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
                    await RecalculateSaleBalanceIncludingReturnsAsync(sale, userId, saleReturn.StockMainID);
                    _stockMainRepository.Update(sale);
                }
            }

            _stockMainRepository.Update(saleReturn);
            await _unitOfWork.SaveChangesAsync();

            return true;
        });
    }
}
