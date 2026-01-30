using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Domain.Entities.Configuration;

namespace PharmaCare.Application.Implementations.Configuration;

/// <summary>
/// Service implementation for Product entity operations
/// </summary>
public class ProductService : IProductService
{
    private readonly IRepository<Product> _repository;
    private readonly IRepository<SubCategory> _subCategoryRepository;
    private readonly IRepository<Category> _categoryRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ProductService(
        IRepository<Product> repository,
        IRepository<SubCategory> subCategoryRepository,
        IRepository<Category> categoryRepository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _subCategoryRepository = subCategoryRepository;
        _categoryRepository = categoryRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<Product>> GetAllAsync()
    {
        return await _repository.Query()
            .Include(p => p.SubCategory)
            .Include(p => p.Category)
            .OrderByDescending(p => p.IsActive)
            .ThenBy(p => p.Name)
            .ToListAsync();
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
        product.Code = await GenerateProductCodeAsync();
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
        existing.Barcode = product.Barcode;
        existing.Name = product.Name;
        existing.Barcode = product.Barcode;
        existing.Category_ID = product.Category_ID;
        existing.SubCategory_ID = product.SubCategory_ID;
        existing.CostPrice = product.CostPrice;
        existing.SellingPrice = product.SellingPrice;
        existing.ReorderLevel = product.ReorderLevel;
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

    public async Task<string> GenerateProductCodeAsync()
    {
        var lastProduct = await _repository.Query()
            .OrderByDescending(p => p.ProductID)
            .FirstOrDefaultAsync();

        int nextNumber = (lastProduct?.ProductID ?? 0) + 1;
        return $"PRD-{nextNumber:D4}";
    }
}
