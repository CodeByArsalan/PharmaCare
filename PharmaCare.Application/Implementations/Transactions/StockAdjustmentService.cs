using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.DTOs;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Domain.Entities.Accounting;
using PharmaCare.Domain.Entities.Configuration;
using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.Application.Implementations.Transactions;

public class StockAdjustmentService : TransactionServiceBase, IStockAdjustmentService
{
    private const string PREFIX = "ADJ";
    private const string JV_VOUCHER_CODE = "JV";
    private const string TRAN_TYPE_IN = "SADJ+";
    private const string TRAN_TYPE_OUT = "SADJ-";

    private readonly IRepository<TransactionType> _transactionTypeRepository;
    private readonly IRepository<VoucherType> _voucherTypeRepository;
    private readonly IRepository<Category> _categoryRepository;
    private readonly IRepository<Product> _productRepository;
    private readonly IProductService _productService;

    public StockAdjustmentService(
        IRepository<StockMain> stockMainRepository,
        IRepository<TransactionType> transactionTypeRepository,
        IRepository<Voucher> voucherRepository,
        IRepository<VoucherType> voucherTypeRepository,
        IRepository<Category> categoryRepository,
        IRepository<Product> productRepository,
        IProductService productService,
        IUnitOfWork unitOfWork,
        IFinancialPeriodService financialPeriodService)
        : base(stockMainRepository, voucherRepository, unitOfWork, financialPeriodService)
    {
        _transactionTypeRepository = transactionTypeRepository;
        _voucherTypeRepository = voucherTypeRepository;
        _categoryRepository = categoryRepository;
        _productRepository = productRepository;
        _productService = productService;
    }

    public async Task<PagedResult<StockMain>> GetPagedAsync(string? type = null, DateTime? from = null, DateTime? to = null, int page = 1, int pageSize = 10)
    {
        var ttIn = await _transactionTypeRepository.Query().FirstOrDefaultAsync(t => t.Code == TRAN_TYPE_IN);
        var ttOut = await _transactionTypeRepository.Query().FirstOrDefaultAsync(t => t.Code == TRAN_TYPE_OUT);

        var query = _stockMainRepository.Query()
            .Include(m => m.TransactionType)
            .Include(m => m.StockDetails)
                .ThenInclude(d => d.Product)
            .Where(m => m.TransactionType_ID == ttIn!.TransactionTypeID || m.TransactionType_ID == ttOut!.TransactionTypeID);

        if (!string.IsNullOrEmpty(type))
        {
            query = query.Where(m => m.AdjustmentType == type);
        }

        if (from.HasValue) query = query.Where(m => m.TransactionDate.Date >= from.Value.Date);
        if (to.HasValue) query = query.Where(m => m.TransactionDate.Date <= to.Value.Date);

        var totalCount = await query.CountAsync();
        var items = await query.OrderByDescending(m => m.TransactionDate)
                              .ThenByDescending(m => m.StockMainID)
                              .Skip((page - 1) * pageSize)
                              .Take(pageSize)
                              .ToListAsync();

        return new PagedResult<StockMain>
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
            .Include(m => m.TransactionType)
            .Include(m => m.Voucher)
                .ThenInclude(v => v!.VoucherDetails)
            .Include(m => m.StockDetails)
                .ThenInclude(d => d.Product)
            .FirstOrDefaultAsync(m => m.StockMainID == id);
    }

    public async Task<StockMain> CreateAsync(StockMain adjustment, int userId)
    {
        await ValidatePeriodAsync(adjustment.TransactionDate);

        return await ExecuteInTransactionAsync(async () =>
        {
            if (string.IsNullOrEmpty(adjustment.AdjustmentType) || 
                (adjustment.AdjustmentType != "Write-off" && adjustment.AdjustmentType != "Write-in"))
                throw new InvalidOperationException("Invalid adjustment type.");

            if (!adjustment.StockDetails.Any())
                throw new InvalidOperationException("Adjustment must have at least one product.");

            var isWriteIn = adjustment.AdjustmentType == "Write-in";
            var tranTypeCode = isWriteIn ? TRAN_TYPE_IN : TRAN_TYPE_OUT;
            var transactionType = await _transactionTypeRepository.Query()
                .FirstOrDefaultAsync(t => t.Code == tranTypeCode);
                
            if (transactionType == null)
                throw new InvalidOperationException($"Transaction Type '{tranTypeCode}' not found.");

            adjustment.TransactionType_ID = transactionType.TransactionTypeID;
            adjustment.TransactionNo = await GenerateTransactionNoAsync(PREFIX);
            adjustment.Status = "Approved";
            adjustment.CreatedAt = AppTime.Now;
            adjustment.CreatedBy = userId;

            // Fetch current stock, costs, and categories
            var productIds = adjustment.StockDetails.Select(d => d.Product_ID).ToList();

            // Serialize against concurrent stock movements so the write-off availability
            // check below operates on stock that cannot change under us.
            await LockProductStockAsync(productIds);

            var stockDict = await _productService.GetStockStatusAsync(productIds);
            var costPrices = await _productService.GetLastGrnCostPricesAsync(productIds);
            
            // Categories needed for accounting rules
            var products = await _productRepository.Query().Where(p => productIds.Contains(p.ProductID)).ToListAsync();
            var productsList = products.ToList();
            var categoryIds = productsList.Where(p => p.Category_ID.HasValue).Select(p => p.Category_ID!.Value).Distinct().ToList();
            var categories = await _categoryRepository.Query().Where(c => categoryIds.Contains(c.CategoryID)).ToListAsync();

            decimal totalAmount = 0;
            var voucherDetails = new List<VoucherDetail>();

            foreach (var detail in adjustment.StockDetails)
            {
                if (detail.Quantity <= 0)
                    throw new InvalidOperationException("Quantity must be greater than zero.");

                if (!isWriteIn)
                {
                    // Write-off validation
                    var currentStock = stockDict.ContainsKey(detail.Product_ID) ? stockDict[detail.Product_ID] : 0;
                    if (detail.Quantity > currentStock)
                        throw new InvalidOperationException($"Insufficient stock for product ID {detail.Product_ID}. Current stock is {currentStock}.");
                }

                // Accounting logic per detail line
                var product = productsList.First(p => p.ProductID == detail.Product_ID);
                if (product.Category_ID == null)
                    throw new InvalidOperationException($"Product '{product.Name}' has no category assigned.");

                // Cost basis: last approved GRN cost, falling back to the product's opening price
                // (never 0 — a zero valuation would move stock with no accounting record).
                var cost = costPrices.TryGetValue(detail.Product_ID, out var grnCost) && grnCost > 0
                    ? grnCost
                    : product.OpeningPrice;

                if (cost <= 0)
                    throw new InvalidOperationException(
                        $"Cannot value '{product.Name}': no GRN cost or opening price is set. Enter a cost before adjusting.");

                var lineTotal = cost * detail.Quantity;
                detail.CostPrice = cost;
                detail.LineCost = lineTotal;
                detail.LineTotal = lineTotal; // Used as valuation representation
                totalAmount += lineTotal;
                    
                var category = categories.FirstOrDefault(c => c.CategoryID == product.Category_ID);
                if (category == null || category.DamageAccount_ID == null || category.StockAccount_ID == null)
                    throw new InvalidOperationException($"Category for '{product.Name}' is missing Stock or Damage accounts linking.");

                if (isWriteIn)
                {
                    // Write-In (Increase stock): DR Stock Account, CR Damage Account (Reversal of loss / Adjustment Income)
                    // Aggregate details to voucher
                    AddOrUpdateVoucherDetail(voucherDetails, category.StockAccount_ID.Value, lineTotal, 0, $"Stock Write-In - {product.Name}");
                    AddOrUpdateVoucherDetail(voucherDetails, category.DamageAccount_ID.Value, 0, lineTotal, $"Stock Write-In - {product.Name}");
                }
                else
                {
                    // Write-Off (Decrease stock): DR Damage Account (Loss Expense), CR Stock Account
                    AddOrUpdateVoucherDetail(voucherDetails, category.DamageAccount_ID.Value, lineTotal, 0, $"Stock Loss - {product.Name}");
                    AddOrUpdateVoucherDetail(voucherDetails, category.StockAccount_ID.Value, 0, lineTotal, $"Stock Loss - {product.Name}");
                }
            }

            adjustment.SubTotal = totalAmount;
            adjustment.TotalAmount = totalAmount;

            // Accounting Voucher Creation. Cost is enforced > 0 per line above, so a stock-moving
            // adjustment always has a non-zero valuation and must post a voucher — never skip it.
            var voucherType = await _voucherTypeRepository.Query().FirstOrDefaultAsync(vt => vt.Code == JV_VOUCHER_CODE);
            if (voucherType == null)
                throw new InvalidOperationException($"Voucher type '{JV_VOUCHER_CODE}' not found.");

            Voucher? adjustmentVoucher = null;
            if (totalAmount > 0)
            {
                var voucher = new Voucher
                {
                    VoucherType_ID = voucherType.VoucherTypeID,
                    VoucherNo = await GenerateVoucherNoAsync(JV_VOUCHER_CODE),
                    VoucherDate = adjustment.TransactionDate,
                    TotalDebit = totalAmount,
                    TotalCredit = totalAmount,
                    Status = "Posted",
                    SourceTable = "StockMain",
                    Narration = $"Stock {adjustment.AdjustmentType}: {adjustment.AdjustmentReason} ({adjustment.TransactionNo})",
                    CreatedAt = AppTime.Now,
                    CreatedBy = userId,
                    VoucherDetails = voucherDetails
                };
                await _voucherRepository.AddAsync(voucher);
                adjustment.Voucher = voucher;
                adjustmentVoucher = voucher;
            }

            await _stockMainRepository.AddAsync(adjustment);
            await _unitOfWork.SaveChangesAsync();

            // SourceTable + SourceID is how a voucher is traced back to the record that caused it,
            // and every other transaction service stamps both. The id only exists after the insert
            // above, so stamp it here — still inside the enclosing transaction.
            if (adjustmentVoucher != null)
            {
                adjustmentVoucher.SourceID = adjustment.StockMainID;
                _voucherRepository.Update(adjustmentVoucher);
                await _unitOfWork.SaveChangesAsync();
            }

            return adjustment;
        });
    }

    public async Task<bool> VoidAsync(int id, string reason, int userId)
    {
        return await ExecuteInTransactionAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new InvalidOperationException("Void reason is required.");

            var adj = await _stockMainRepository.Query()
                .Include(m => m.Voucher)
                    .ThenInclude(v => v!.VoucherDetails)
                .FirstOrDefaultAsync(m => m.StockMainID == id);

            if (adj == null || adj.Status == "Void")
                return false;

            await ValidatePeriodAsync(adj.TransactionDate);

            // For write-In, we need to ensure voiding it (which decreases stock) won't result in negative stock
            var ttIn = await _transactionTypeRepository.Query().FirstOrDefaultAsync(t => t.Code == TRAN_TYPE_IN);
            if (adj.TransactionType_ID == ttIn?.TransactionTypeID)
            {
                var details = await _stockMainRepository.Query()
                    .Where(m => m.StockMainID == id)
                    .SelectMany(m => m.StockDetails)
                    .ToListAsync();
                    
                var productIds = details.Select(d => d.Product_ID).Distinct().ToList();
                await LockProductStockAsync(productIds); // race-safe: serialize vs concurrent sales
                var stockDict = await _productService.GetStockStatusAsync(productIds);
                foreach (var detail in details)
                {
                    var currentStock = stockDict.ContainsKey(detail.Product_ID) ? stockDict[detail.Product_ID] : 0;
                    if (detail.Quantity > currentStock)
                        throw new InvalidOperationException($"Cannot void: Insufficient stock for product ID {detail.Product_ID}.");
                }
            }

            adj.Status = "Void";
            adj.VoidReason = reason.Trim();
            adj.VoidedAt = AppTime.Now;
            adj.VoidedBy = userId;
            adj.UpdatedAt = AppTime.Now;
            adj.UpdatedBy = userId;
            _stockMainRepository.Update(adj);

            if (adj.Voucher_ID.HasValue && adj.Voucher != null && !adj.Voucher.IsReversed)
            {
                await CreateReversalVoucherAsync(adj.Voucher_ID, userId, reason, "StockMain", adj.StockMainID);
            }

            await _unitOfWork.SaveChangesAsync();
            return true;
        });
    }

    private void AddOrUpdateVoucherDetail(List<VoucherDetail> list, int accountId, decimal dr, decimal cr, string description)
    {
        var existing = list.FirstOrDefault(x => x.Account_ID == accountId);
        if (existing != null)
        {
            existing.DebitAmount += dr;
            existing.CreditAmount += cr;
        }
        else
        {
            list.Add(new VoucherDetail
            {
                Account_ID = accountId,
                DebitAmount = dr,
                CreditAmount = cr,
                Description = description
            });
        }
    }
}
