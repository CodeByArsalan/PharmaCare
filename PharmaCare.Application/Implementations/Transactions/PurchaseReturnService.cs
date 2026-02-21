using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Domain.Entities.Accounting;
using PharmaCare.Domain.Entities.Configuration;
using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.Application.Implementations.Transactions;

/// <summary>
/// Service for managing Purchase Returns with double-entry accounting.
/// Creates PRV vouchers and reversal vouchers on void.
/// </summary>
public class PurchaseReturnService : TransactionServiceBase, IPurchaseReturnService
{
    private readonly IRepository<TransactionType> _transactionTypeRepository;
    private readonly IRepository<VoucherType> _voucherTypeRepository;
    private readonly IRepository<Party> _partyRepository;
    private readonly IRepository<Product> _productRepository;

    private const string TRANSACTION_TYPE_CODE = "PRTN";
    private const string GRN_TRANSACTION_TYPE_CODE = "GRN";
    private const string PREFIX = "PRTN";
    private const string PURCHASE_RETURN_VOUCHER_CODE = "PRV";

    public PurchaseReturnService(
        IRepository<StockMain> stockMainRepository,
        IRepository<TransactionType> transactionTypeRepository,
        IRepository<Voucher> voucherRepository,
        IRepository<VoucherType> voucherTypeRepository,
        IRepository<Party> partyRepository,
        IRepository<Product> productRepository,
        IUnitOfWork unitOfWork)
        : base(stockMainRepository, voucherRepository, unitOfWork)
    {
        _transactionTypeRepository = transactionTypeRepository;
        _voucherTypeRepository = voucherTypeRepository;
        _partyRepository = partyRepository;
        _productRepository = productRepository;
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

    public async Task<StockMain> CreateAsync(StockMain purchaseReturn, int userId)
    {
        // Get the PRTN transaction type
        var transactionType = await _transactionTypeRepository.Query()
            .FirstOrDefaultAsync(t => t.Code == TRANSACTION_TYPE_CODE);

        if (transactionType == null)
            throw new InvalidOperationException($"Transaction type '{TRANSACTION_TYPE_CODE}' not found.");

        purchaseReturn.TransactionType_ID = transactionType.TransactionTypeID;
        purchaseReturn.TransactionNo = await GenerateTransactionNoAsync(PREFIX);
        purchaseReturn.Status = "Approved"; // Returns are immediately approved (stock impact)
        purchaseReturn.PaymentStatus = "Unpaid";
        purchaseReturn.CreatedAt = DateTime.Now;
        purchaseReturn.CreatedBy = userId;

        // Calculate totals
        CalculateTotals(purchaseReturn);

        // Server-side quantity validation against the reference GRN
        if (purchaseReturn.ReferenceStockMain_ID.HasValue)
        {
            await ValidateReturnQuantitiesAsync(purchaseReturn);
        }

        await _stockMainRepository.AddAsync(purchaseReturn);
        await _unitOfWork.SaveChangesAsync();

        // Create accounting entries (PRV voucher)
        var voucher = await CreatePurchaseReturnVoucherAsync(purchaseReturn, userId);

        // Link voucher to the purchase return
        purchaseReturn.Voucher_ID = voucher.VoucherID;
        _stockMainRepository.Update(purchaseReturn);

        // Update the reference GRN's balance if linked
        if (purchaseReturn.ReferenceStockMain_ID.HasValue)
        {
            var grn = await _stockMainRepository.Query()
                .FirstOrDefaultAsync(s => s.StockMainID == purchaseReturn.ReferenceStockMain_ID.Value);

            if (grn != null)
            {
                grn.BalanceAmount -= purchaseReturn.TotalAmount;
                if (grn.BalanceAmount < 0) grn.BalanceAmount = 0;
                _stockMainRepository.Update(grn);
            }
        }

        await _unitOfWork.SaveChangesAsync();

        return purchaseReturn;
    }

    /// <summary>
    /// Creates a Purchase Return Voucher (PRV) with double-entry accounting.
    /// This is the REVERSE of the original purchase voucher:
    /// Debit: Supplier Account (Accounts Payable) - reduces liability (you owe less)
    /// Credit: Stock Account(s) (Inventory) - reduces asset (goods leaving inventory)
    /// </summary>
    private async Task<Voucher> CreatePurchaseReturnVoucherAsync(StockMain purchaseReturn, int userId)
    {
        // Get Purchase Return Voucher type (PRV, ID: 7)
        var voucherType = await _voucherTypeRepository.Query()
            .FirstOrDefaultAsync(vt => vt.Code == PURCHASE_RETURN_VOUCHER_CODE);

        if (voucherType == null)
            throw new InvalidOperationException($"Voucher type '{PURCHASE_RETURN_VOUCHER_CODE}' not found.");

        // Get supplier with account
        var supplier = await _partyRepository.Query()
            .Include(p => p.Account)
            .FirstOrDefaultAsync(p => p.PartyID == purchaseReturn.Party_ID);

        if (supplier?.Account_ID == null)
            throw new InvalidOperationException("Supplier does not have an associated account for accounting entries.");

        // Get products with their categories and stock accounts
        var productIds = purchaseReturn.StockDetails.Select(d => d.Product_ID).Distinct().ToList();
        var products = await _productRepository.Query()
            .Include(p => p.Category)
            .Where(p => productIds.Contains(p.ProductID))
            .ToListAsync();

        // Group line items by stock account and sum totals
        var stockAccountTotals = new Dictionary<int, decimal>();
        foreach (var detail in purchaseReturn.StockDetails)
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

        var voucherNo = await GenerateVoucherNoAsync(PURCHASE_RETURN_VOUCHER_CODE);

        var voucher = new Voucher
        {
            VoucherType_ID = voucherType.VoucherTypeID,
            VoucherNo = voucherNo,
            VoucherDate = purchaseReturn.TransactionDate,
            TotalDebit = purchaseReturn.TotalAmount,
            TotalCredit = purchaseReturn.TotalAmount,
            Status = "Posted",
            SourceTable = "StockMain",
            SourceID = purchaseReturn.StockMainID,
            Narration = $"Purchase return to {supplier.Name}. Return: {purchaseReturn.TransactionNo}",
            CreatedAt = DateTime.Now,
            CreatedBy = userId
        };

        // Debit: Supplier Account (reduces Accounts Payable liability)
        voucher.VoucherDetails.Add(new VoucherDetail
        {
            Account_ID = supplier.Account_ID.Value,
            DebitAmount = purchaseReturn.TotalAmount,
            CreditAmount = 0,
            Description = $"Purchase return to {supplier.Name}",
            Party_ID = supplier.PartyID
        });

        // Credit: Stock Account(s) (reduces inventory asset)
        foreach (var stockAccount in stockAccountTotals)
        {
            voucher.VoucherDetails.Add(new VoucherDetail
            {
                Account_ID = stockAccount.Key,
                DebitAmount = 0,
                CreditAmount = stockAccount.Value,
                Description = $"Inventory returned - {purchaseReturn.TransactionNo}"
            });
        }

        await _voucherRepository.AddAsync(voucher);
        await _unitOfWork.SaveChangesAsync();

        return voucher;
    }

    public async Task<IEnumerable<StockMain>> GetGrnsForReturnAsync(int? supplierId = null)
    {
        var query = _stockMainRepository.Query()
            .Include(s => s.TransactionType)
            .Include(s => s.Party)
            .Include(s => s.StockDetails)
                .ThenInclude(d => d.Product)
            .Where(s => s.TransactionType!.Code == GRN_TRANSACTION_TYPE_CODE 
                     && s.Status == "Approved"
                     && s.BalanceAmount > 0);

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
        var purchaseReturn = await GetByIdAsync(id);
        if (purchaseReturn == null)
            return false;

        if (purchaseReturn.Status == "Void")
            return false;

        purchaseReturn.Status = "Void";
        purchaseReturn.VoidReason = reason;
        purchaseReturn.VoidedAt = DateTime.Now;
        purchaseReturn.VoidedBy = userId;

        // Create reversal voucher if original voucher exists
        if (purchaseReturn.Voucher_ID.HasValue)
        {
            // Use base method to create reversal voucher
            await CreateReversalVoucherAsync(purchaseReturn.Voucher_ID.Value, userId, reason, "StockMain", purchaseReturn.StockMainID);
        }

        // Restore the reference GRN's balance if linked
        if (purchaseReturn.ReferenceStockMain_ID.HasValue)
        {
            var grn = await _stockMainRepository.Query()
                .FirstOrDefaultAsync(s => s.StockMainID == purchaseReturn.ReferenceStockMain_ID.Value);

            if (grn != null)
            {
                grn.BalanceAmount += purchaseReturn.TotalAmount;
                _stockMainRepository.Update(grn);
            }
        }

        _stockMainRepository.Update(purchaseReturn);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }

    /// <summary>
    /// Validates that return quantities do not exceed the original GRN quantities
    /// minus any quantities already returned in previous non-voided returns.
    /// </summary>
    private async Task ValidateReturnQuantitiesAsync(StockMain purchaseReturn)
    {
        // Get the reference GRN with its details
        var grn = await _stockMainRepository.Query()
            .Include(s => s.StockDetails)
            .FirstOrDefaultAsync(s => s.StockMainID == purchaseReturn.ReferenceStockMain_ID!.Value);

        if (grn == null)
            throw new InvalidOperationException("Reference GRN not found.");

        // Get all existing non-voided returns against this GRN
        var existingReturns = await _stockMainRepository.Query()
            .Include(s => s.TransactionType)
            .Include(s => s.StockDetails)
            .Where(s => s.TransactionType!.Code == TRANSACTION_TYPE_CODE
                     && s.ReferenceStockMain_ID == grn.StockMainID
                     && s.Status != "Void")
            .ToListAsync();

        // Calculate already-returned quantities per product
        var alreadyReturned = new Dictionary<int, decimal>();
        foreach (var ret in existingReturns)
        {
            foreach (var detail in ret.StockDetails)
            {
                if (alreadyReturned.ContainsKey(detail.Product_ID))
                    alreadyReturned[detail.Product_ID] += detail.Quantity;
                else
                    alreadyReturned[detail.Product_ID] = detail.Quantity;
            }
        }

        // Validate each line item in the new return
        foreach (var detail in purchaseReturn.StockDetails)
        {
            var grnDetail = grn.StockDetails.FirstOrDefault(d => d.Product_ID == detail.Product_ID);
            if (grnDetail == null)
                throw new InvalidOperationException($"Product ID {detail.Product_ID} was not found in the reference GRN.");

            var previouslyReturned = alreadyReturned.ContainsKey(detail.Product_ID) 
                ? alreadyReturned[detail.Product_ID] 
                : 0;

            var availableForReturn = grnDetail.Quantity - previouslyReturned;

            if (detail.Quantity > availableForReturn)
                throw new InvalidOperationException(
                    $"Return quantity ({detail.Quantity}) for product '{grnDetail.Product?.Name ?? $"ID:{detail.Product_ID}"}' " +
                    $"exceeds available quantity ({availableForReturn}). " +
                    $"GRN Qty: {grnDetail.Quantity}, Already Returned: {previouslyReturned}.");
        }
    }
}
