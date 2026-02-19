using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.DTOs.Configuration;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Domain.Entities.Configuration;
using PharmaCare.Domain.Entities.Transactions;

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
    private readonly IRepository<StockDetail> _stockDetailRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ProductService(
        IRepository<Product> repository,
        IRepository<SubCategory> subCategoryRepository,
        IRepository<Category> categoryRepository,
        IRepository<PriceType> priceTypeRepository,
        IRepository<ProductPrice> productPriceRepository,
        IRepository<StockDetail> stockDetailRepository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _subCategoryRepository = subCategoryRepository;
        _categoryRepository = categoryRepository;
        _priceTypeRepository = priceTypeRepository;
        _productPriceRepository = productPriceRepository;
        _stockDetailRepository = stockDetailRepository;
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
        query = query.OrderByDescending(p => p.ProductID);

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

    public async Task<Product?> GetByIdAsync(int id)
    {
        return await _repository.Query()
            .Include(p => p.Category)
            .Include(p => p.SubCategory)
            .FirstOrDefaultAsync(p => p.ProductID == id);
    }

    public async Task<Product> CreateAsync(Product product, int userId)
    {
        product.CreatedAt = DateTime.Now;
        product.CreatedBy = userId;
        product.IsActive = true;

        await _repository.AddAsync(product);
        await _unitOfWork.SaveChangesAsync();
        
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
        existing.UpdatedAt = DateTime.Now;
        existing.UpdatedBy = userId;

        _repository.Update(existing);
        await _unitOfWork.SaveChangesAsync();
        
        return true;
    }

    public async Task<bool> ToggleStatusAsync(int id, int userId)
    {
        var product = await GetByIdAsync(id);
        if (product == null)
            return false;

        product.IsActive = !product.IsActive;
        product.UpdatedAt = DateTime.Now;
        product.UpdatedBy = userId;

        _repository.Update(product);
        await _unitOfWork.SaveChangesAsync();
        
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

    public async Task SaveProductPricesAsync(int productId, List<ProductPriceDto> prices, int userId)
    {
        var existingPrices = await _productPriceRepository.Query()
            .Where(pp => pp.Product_ID == productId)
            .ToListAsync();

        foreach (var priceDto in prices)
        {
            var existingPrice = existingPrices.FirstOrDefault(pp => pp.PriceType_ID == priceDto.PriceTypeId);

            if (priceDto.Price > 0)
            {
                if (existingPrice != null)
                {
                    existingPrice.SalePrice = priceDto.Price;
                    existingPrice.IsActive = true;
                    existingPrice.UpdatedAt = DateTime.Now;
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
                        CreatedAt = DateTime.Now,
                        CreatedBy = userId
                    };
                    await _productPriceRepository.AddAsync(newPrice);
                }
            }
            else
            {
                if (existingPrice != null && existingPrice.IsActive)
                {
                    existingPrice.IsActive = false;
                    existingPrice.UpdatedAt = DateTime.Now;
                    existingPrice.UpdatedBy = userId;
                    _productPriceRepository.Update(existingPrice);
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

        // Get all stock movements for approved transactions
        var stockMovements = await _stockDetailRepository.Query()
            .Include(sd => sd.StockMain)
                .ThenInclude(sm => sm!.TransactionType)
            .Where(sd => sd.StockMain!.Status == "Approved" && sd.StockMain.TransactionType!.AffectsStock)
            .GroupBy(sd => sd.Product_ID)
            .Select(g => new
            {
                ProductId = g.Key,
                StockChange = g.Sum(sd => sd.Quantity * sd.StockMain!.TransactionType!.StockDirection)
            })
            .ToListAsync();

        var stockDict = stockMovements.ToDictionary(x => x.ProductId, x => x.StockChange);

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
}
