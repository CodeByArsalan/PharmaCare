using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Domain.Models.Products;
using PharmaCare.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using PharmaCare.Domain.Models.Configuration;
using PharmaCare.Infrastructure;

namespace PharmaCare.Application.Implementations.Configuration;

public class ProductService(IRepository<Product> _productRepository, IRepository<SubCategory> _subCategoryRepository, PharmaCareDBContext _dbContext) : IProductService
{
    public async Task<List<Product>> GetProducts()
    {
        return await _dbContext.Products
            .Include(p => p.SubCategory)
                .ThenInclude(sc => sc!.Category)
            .ToListAsync();
    }

    public async Task<Product?> GetProductById(int productId)
    {
        return await _productRepository.FindByConditionWithInclude(p => p.ProductID == productId, [p => p.SubCategory])
            .FirstOrDefaultAsync();
    }

    public async Task<bool> CreateProduct(Product product)
    {
        // Auto-generate ProductCode if empty? Or is it user input? 
        // User didn't specify auto-generation for ProductCode, so assuming input.
        // But usually codes are generated. For now, we trust the input object.
        product.IsActive = true;
        product.CreatedDate = DateTime.Now;
        return await _productRepository.Insert(product);
    }

    public async Task<bool> UpdateProduct(Product product)
    {
        var existingProduct = _productRepository.GetById(product.ProductID);
        if (existingProduct == null) return false;

        existingProduct.Sku = product.Sku;
        existingProduct.Barcode = product.Barcode;
        existingProduct.ReorderLevel = product.ReorderLevel;
        existingProduct.ProductName = product.ProductName;
        existingProduct.ProductCode = product.ProductCode;
        existingProduct.OpeningPrice = product.OpeningPrice;
        existingProduct.OpeningQuantity = product.OpeningQuantity;
        existingProduct.SubCategory_ID = product.SubCategory_ID;
        existingProduct.UpdatedBy = product.UpdatedBy;
        existingProduct.UpdatedDate = DateTime.Now;

        return await _productRepository.Update(existingProduct, null); // Assuming null updates all or handled by repo implementation
    }

    public async Task<bool> DeleteProduct(int productId)
    {
        var product = _productRepository.GetById(productId);
        if (product == null) return false;

        product.IsActive = !product.IsActive; // Toggle status
        product.UpdatedDate = DateTime.Now;
        return await _productRepository.Update(product, null);
    }

    public async Task<List<SubCategory>> GetSubCategories()
    {
        return (await _subCategoryRepository.GetAll()).ToList();
    }
}
