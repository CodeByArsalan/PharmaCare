using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.DTOs;
using PharmaCare.Application.DTOs.Configuration;
using PharmaCare.Application.Exceptions;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Utilities;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Domain.Entities.Configuration;
using PharmaCare.Domain.Entities.Transactions;
using PharmaCare.Application.Interfaces.Logging;
using PharmaCare.Domain.Enums;
using System.Text.Json;

namespace PharmaCare.Application.Implementations.Configuration;

/// <summary>
/// Service implementation for Product entity operations
/// </summary>
public class ProductService : IProductService
{
    private readonly IRepository<Product> _repository;
    private readonly IRepository<SubCategory> _subCategoryRepository;
    private readonly IRepository<Category> _categoryRepository;
    private readonly IRepository<PriceType> _priceTypeRepository;
    private readonly IRepository<ProductPrice> _productPriceRepository;
    private readonly IRepository<ProductPriceHistory> _productPriceHistoryRepository;
    private readonly IRepository<StockDetail> _stockDetailRepository;
    private readonly IRepository<TransactionType> _transactionTypeRepository;
    private readonly IActivityLogService _activityLogService;
    private readonly ISessionService _sessionService;
    private readonly IUnitOfWork _unitOfWork;

    public ProductService(
        IRepository<Product> repository,
        IRepository<SubCategory> subCategoryRepository,
        IRepository<Category> categoryRepository,
        IRepository<PriceType> priceTypeRepository,
        IRepository<ProductPrice> productPriceRepository,
        IRepository<ProductPriceHistory> productPriceHistoryRepository,
        IRepository<StockDetail> stockDetailRepository,
        IRepository<TransactionType> transactionTypeRepository,
        IActivityLogService activityLogService,
        ISessionService sessionService,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _subCategoryRepository = subCategoryRepository;
        _categoryRepository = categoryRepository;
        _priceTypeRepository = priceTypeRepository;
        _productPriceRepository = productPriceRepository;
        _productPriceHistoryRepository = productPriceHistoryRepository;
        _stockDetailRepository = stockDetailRepository;
        _transactionTypeRepository = transactionTypeRepository;
        _activityLogService = activityLogService;
        _sessionService = sessionService;
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<Product>> GetAllAsync()
    {
        // Default behavior: Top 50 newest products
        return await GetFilteredProductsAsync(null, null, null, null);
    }

    public async Task<IEnumerable<Product>> GetFilteredProductsAsync(int? categoryId, int? subCategoryId, bool? isActive, string? searchTerm)
    {
        var query = _repository.Query()
            .Include(p => p.SubCategory)
            .Include(p => p.Category)
            .AsQueryable();

        if (categoryId.HasValue)
        {
            query = query.Where(p => p.Category_ID == categoryId.Value);
        }

        if (subCategoryId.HasValue)
        {
            query = query.Where(p => p.SubCategory_ID == subCategoryId.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(p => p.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            searchTerm = searchTerm.ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(searchTerm) || 
                                     (p.ShortCode != null && p.ShortCode.ToLower().Contains(searchTerm)));
        }

        // Apply sorting: Order by ProductID descending (newest first)
        query = query.OrderByDescending(p => p.IsActive);

        // Limit results if no search/filter to verify "Top 50" requirement for default view
        // But users might want to see all filtered results. 
        // Instructions said: "Default Display: Initially display the top 50 products"
        // So applied only when no filters are present? Or always top 50?
        // Usually filtering implies seeing all matches. Limiting might confuse.
        // I will limit to 50 only if no filters are provided (initial load).
        
        bool hasFilters = categoryId.HasValue || subCategoryId.HasValue || isActive.HasValue || !string.IsNullOrWhiteSpace(searchTerm);

        if (!hasFilters)
        {
            query = query.Take(50);
        }

        return await query.ToListAsync();
    }

    public async Task<PagedResult<Product>> GetPagedFilteredProductsAsync(int? categoryId, int? subCategoryId, bool? isActive, string? searchTerm, int page, int pageSize)
    {
        var query = _repository.Query()
            .AsNoTracking()
            .Include(p => p.SubCategory)
            .Include(p => p.Category)
            .AsQueryable();

        if (categoryId.HasValue)
            query = query.Where(p => p.Category_ID == categoryId.Value);

        if (subCategoryId.HasValue)
            query = query.Where(p => p.SubCategory_ID == subCategoryId.Value);

        if (isActive.HasValue)
            query = query.Where(p => p.IsActive == isActive.Value);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(term) ||
                                     (p.ShortCode != null && p.ShortCode.ToLower().Contains(term)));
        }

        int totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(p => p.IsActive)
            .ThenByDescending(p => p.ProductID)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<Product>
        {
            Items = items,
            TotalCount = totalCount,
            CurrentPage = page,
            PageSize = pageSize
        };
    }

    public async Task<Product?> GetByIdAsync(int id)
    {
        return await _repository.Query()
            .Include(p => p.Category)
            .Include(p => p.SubCategory)
            .FirstOrDefaultAsync(p => p.ProductID == id);
    }

    public async Task<Product> CreateAsync(Product product, int userId)
    {
        product.CreatedAt = AppTime.Now;
        product.CreatedBy = userId;
        product.IsActive = true;

        await _repository.AddAsync(product);
        await _unitOfWork.SaveChangesAsync();
        
        var userName = _sessionService.GetCurrentUser()?.FullName ?? "Unknown User";
        await _activityLogService.LogActivityAsync(
            userId,
            userName,
            ActivityType.Create,
            "Product",
            product.ProductID.ToString(),
            null,
            null,
            $"Created product: {product.Name} (SKU: {product.ShortCode})");

        return product;
    }

    public async Task<bool> UpdateAsync(Product product, int userId)
    {
        var existing = await GetByIdAsync(product.ProductID);
        if (existing == null)
            return false;

        existing.Name = product.Name;
        existing.ShortCode = product.ShortCode;
        existing.Category_ID = product.Category_ID;
        existing.SubCategory_ID = product.SubCategory_ID;
        existing.OpeningPrice = product.OpeningPrice;
        existing.OpeningQuantity = product.OpeningQuantity;
        existing.ReorderLevel = product.ReorderLevel;
        existing.UnitsInPack = product.UnitsInPack;
        existing.IsActive = product.IsActive;
        existing.UpdatedAt = AppTime.Now;
        existing.UpdatedBy = userId;

        _repository.Update(existing);
        await _unitOfWork.SaveChangesAsync();
        
        var userName = _sessionService.GetCurrentUser()?.FullName ?? "Unknown User";
        await _activityLogService.LogActivityAsync(
            userId,
            userName,
            ActivityType.Update,
            "Product",
            existing.ProductID.ToString(),
            null, // Interceptor handles JSON values
            null,
            $"Updated product: {existing.Name} (SKU: {existing.ShortCode})");

        return true;
    }

    public async Task<bool> ToggleStatusAsync(int id, int userId)
    {
        var product = await GetByIdAsync(id);
        if (product == null)
            return false;

        product.IsActive = !product.IsActive;
        product.UpdatedAt = AppTime.Now;
        product.UpdatedBy = userId;

        _repository.Update(product);
        await _unitOfWork.SaveChangesAsync();
        
        var userName = _sessionService.GetCurrentUser()?.FullName ?? "Unknown User";
        await _activityLogService.LogActivityAsync(
            userId,
            userName,
            ActivityType.StatusChange,
            "Product",
            product.ProductID.ToString(),
            null,
            null,
            $"{ (product.IsActive ? "Activated" : "Deactivated") } product: {product.Name}");

        return true;
    }

    public async Task<IEnumerable<SubCategory>> GetSubCategoriesForDropdownAsync()
    {
        return await _subCategoryRepository.Query()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Category>> GetCategoriesForDropdownAsync()
    {
        return await _categoryRepository.Query()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<SubCategory>> GetSubCategoriesByCategoryIdAsync(int categoryId)
    {
        return await _subCategoryRepository.Query()
            .Where(s => s.Category_ID == categoryId && s.IsActive)
            .OrderBy(s => s.Name)
            .ToListAsync();
    }



    public async Task<IEnumerable<PriceType>> GetPriceTypesAsync()
    {
        return await _priceTypeRepository.Query()
            .Where(pt => pt.IsActive)
            .OrderBy(pt => pt.PriceTypeID)
            .ToListAsync();
    }

    public async Task<IEnumerable<ProductPrice>> GetProductPricesAsync(int productId)
    {
        return await _productPriceRepository.Query()
            .Include(pp => pp.PriceType)
            .Where(pp => pp.Product_ID == productId && pp.IsActive)
            .ToListAsync();
    }

    public async Task<IEnumerable<ProductPriceHistory>> GetPriceHistoryAsync(int productId)
    {
        return await _productPriceHistoryRepository.Query()
            .Include(h => h.PriceType)
            .Where(h => h.Product_ID == productId)
            .OrderByDescending(h => h.EffectiveFrom)
            .ThenBy(h => h.PriceType_ID)
            .ToListAsync();
    }

    public async Task SaveProductPricesAsync(int productId, List<ProductPriceDto> prices, int userId)
    {
        // PriceType id 2 == Wholesale, whose SalePrice is stored per box (not per unit).
        const int wholesalePriceTypeId = AccountingConstants.WholesalePriceTypeId;

        var product = await _repository.Query().FirstOrDefaultAsync(p => p.ProductID == productId);
        if (product == null)
        {
            throw new PricingValidationException("Product not found.");
        }
        var unitsInPack = product.UnitsInPack < 1 ? 1 : product.UnitsInPack;

        // Authoritative current cost, used both for the below-cost block and to stamp price history.
        var costs = await GetLastGrnCostPricesAsync();
        var cost = costs.TryGetValue(productId, out var c) ? c : product.OpeningPrice;

        var existingPrices = await _productPriceRepository.Query()
            .Where(pp => pp.Product_ID == productId)
            .ToListAsync();

        var openHistory = await _productPriceHistoryRepository.Query()
            .Where(h => h.Product_ID == productId && h.EffectiveTo == null)
            .ToListAsync();

        var now = AppTime.Now;

        foreach (var priceDto in prices)
        {
            var existingPrice = existingPrices.FirstOrDefault(pp => pp.PriceType_ID == priceDto.PriceTypeId);
            var openRow = openHistory.FirstOrDefault(h => h.PriceType_ID == priceDto.PriceTypeId);

            if (priceDto.Price > 0)
            {
                // Margin floor (hard block): reject a sale price that is below cost.
                var perUnitPrice = priceDto.PriceTypeId == wholesalePriceTypeId
                    ? priceDto.Price / unitsInPack
                    : priceDto.Price;
                if (perUnitPrice < cost)
                {
                    throw new PricingValidationException(
                        $"{priceDto.PriceTypeName} price {priceDto.Price:N2} is below the current cost of {cost:N2} per unit. Below-cost prices are not allowed.");
                }

                if (existingPrice != null)
                {
                    existingPrice.SalePrice = priceDto.Price;
                    existingPrice.IsActive = true;
                    existingPrice.UpdatedAt = now;
                    existingPrice.UpdatedBy = userId;
                    _productPriceRepository.Update(existingPrice);
                }
                else
                {
                    var newPrice = new ProductPrice
                    {
                        Product_ID = productId,
                        PriceType_ID = priceDto.PriceTypeId,
                        SalePrice = priceDto.Price,
                        IsActive = true,
                        CreatedAt = now,
                        CreatedBy = userId
                    };
                    await _productPriceRepository.AddAsync(newPrice);
                }

                // Effective-dated history: only append when the price actually changes.
                if (openRow == null || openRow.SalePrice != priceDto.Price)
                {
                    if (openRow != null)
                    {
                        openRow.EffectiveTo = now;
                        _productPriceHistoryRepository.Update(openRow);
                    }

                    await _productPriceHistoryRepository.AddAsync(new ProductPriceHistory
                    {
                        Product_ID = productId,
                        PriceType_ID = priceDto.PriceTypeId,
                        SalePrice = priceDto.Price,
                        CostPriceAtChange = cost,
                        EffectiveFrom = now,
                        EffectiveTo = null,
                        ChangedBy = userId
                    });
                }
            }
            else
            {
                if (existingPrice != null && existingPrice.IsActive)
                {
                    existingPrice.IsActive = false;
                    existingPrice.UpdatedAt = now;
                    existingPrice.UpdatedBy = userId;
                    _productPriceRepository.Update(existingPrice);
                }

                // The explicit price is being removed — close any open history period.
                if (openRow != null)
                {
                    openRow.EffectiveTo = now;
                    _productPriceHistoryRepository.Update(openRow);
                }
            }
        }

        await _unitOfWork.SaveChangesAsync();
    }

    /// <summary>
    /// Gets all active products with calculated current stock.
    /// CurrentStock = OpeningQuantity + SUM(StockDetail.Quantity * TransactionType.StockDirection)
    /// Only considers "Approved" transactions.
    /// If priceTypeId is provided, fetches the specific price for that type.
    /// </summary>
    public async Task<IEnumerable<(Product Product, decimal CurrentStock, decimal? SpecificPrice)>> GetProductsWithStockAsync(int? priceTypeId = null)
    {
        var products = await _repository.Query()
            .Where(p => p.IsActive)
            .ToListAsync();

        // Optimized stock calculation: Fetch IDs first to avoid heavy joins in the aggregation
        var transactionTypes = await _transactionTypeRepository.Query()
            .AsNoTracking()
            .Where(tt => tt.AffectsStock && tt.IsActive)
            .Select(tt => new { tt.TransactionTypeID, tt.StockDirection })
            .ToListAsync();

        var typeIds = transactionTypes.Select(t => t.TransactionTypeID).ToList();
        var directionDict = transactionTypes.ToDictionary(t => t.TransactionTypeID, t => t.StockDirection);

        var stockMovements = await _stockDetailRepository.Query()
            .AsNoTracking()
            .Where(sd => typeIds.Contains(sd.StockMain!.TransactionType_ID) && sd.StockMain.Status == "Approved")
            .GroupBy(sd => new { sd.Product_ID, sd.StockMain!.TransactionType_ID })
            .Select(g => new
            {
                ProductId = g.Key.Product_ID,
                TransactionTypeId = g.Key.TransactionType_ID,
                Quantity = g.Sum(sd => sd.Quantity)
            })
            .ToListAsync();

        var stockDict = stockMovements
            .GroupBy(m => m.ProductId)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(m => m.Quantity * (directionDict.TryGetValue(m.TransactionTypeId, out var dir) ? dir : 0))
            );

        // Get specific prices if priceTypeId is provided
        Dictionary<int, decimal> priceDict = new Dictionary<int, decimal>();
        if (priceTypeId.HasValue)
        {
            var prices = await _productPriceRepository.Query()
                .Where(pp => pp.PriceType_ID == priceTypeId.Value && pp.IsActive)
                .ToListAsync();
            priceDict = prices.ToDictionary(pp => pp.Product_ID, pp => pp.SalePrice);
        }

        return products.Select(p => (
            Product: p,
            CurrentStock: p.OpeningQuantity + (stockDict.TryGetValue(p.ProductID, out var change) ? change : 0),
            SpecificPrice: priceTypeId.HasValue && priceDict.TryGetValue(p.ProductID, out var price) ? price : (decimal?)null
        ));
    }

    public async Task<Dictionary<int, decimal>> GetStockStatusAsync(List<int> productIds)
    {
        if (productIds == null || !productIds.Any())
            return new Dictionary<int, decimal>();

        var products = await _repository.Query()
            .Where(p => productIds.Contains(p.ProductID))
            .Select(p => new { p.ProductID, p.OpeningQuantity })
            .ToListAsync();

        var stockMovements = await _stockDetailRepository.Query()
            .Include(sd => sd.StockMain)
                .ThenInclude(sm => sm!.TransactionType)
            .Where(sd => productIds.Contains(sd.Product_ID) && 
                         sd.StockMain!.Status == "Approved" && 
                         sd.StockMain.TransactionType!.AffectsStock)
            .GroupBy(sd => sd.Product_ID)
            .Select(g => new
            {
                ProductId = g.Key,
                StockChange = g.Sum(sd => sd.Quantity * sd.StockMain!.TransactionType!.StockDirection)
            })
            .ToListAsync();

        var stockDict = stockMovements.ToDictionary(x => x.ProductId, x => x.StockChange);

        var result = new Dictionary<int, decimal>();
        foreach (var p in products)
        {
            var movement = stockDict.TryGetValue(p.ProductID, out var change) ? change : 0;
            result[p.ProductID] = p.OpeningQuantity + movement;
        }

        return result;
    }

    public async Task<Dictionary<int, decimal>> GetLastGrnCostPricesAsync()
    {
        var products = await _repository.Query()
            .Select(p => new { p.ProductID, p.OpeningPrice })
            .ToListAsync();

        // Optimized GRN cost lookup
        var latestGrnCosts = await _stockDetailRepository.Query()
            .Where(sd => sd.StockMain!.Status == "Approved" && sd.StockMain.TransactionType!.Code == "GRN")
            .GroupBy(sd => sd.Product_ID)
            .Select(g => new
            {
                ProductId = g.Key,
                // Using Max(StockMainID) as chronological order of GRNs
                LatestGrnCost = g.OrderByDescending(x => x.StockMain_ID).Select(x => (decimal?)x.CostPrice).FirstOrDefault()
            })
            .ToListAsync();

        var grnDict = latestGrnCosts.ToDictionary(x => x.ProductId, x => x.LatestGrnCost);

        var result = new Dictionary<int, decimal>();
        foreach (var p in products)
        {
            // Fallback to opening price if no GRN exists or cost is null
            result[p.ProductID] = (grnDict.TryGetValue(p.ProductID, out var grnCost) && grnCost.HasValue) ? grnCost.Value : p.OpeningPrice;
        }

        return result;
    }
}
