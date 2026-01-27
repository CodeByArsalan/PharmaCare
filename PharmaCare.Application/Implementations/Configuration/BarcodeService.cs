using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Domain.Models.Products;
using PharmaCare.Infrastructure.Interfaces;

namespace PharmaCare.Application.Implementations.Configuration;

public class BarcodeService(IRepository<Product> _productRepo) : IBarcodeService
{
    public string GenerateProductBarcode(int productId)
    {
        // Simple implementation: Use product ID + checksum
        // For production, use EAN-13 or Code-128 standard
        var baseCode = productId.ToString().PadLeft(12, '0');
        var checksum = CalculateChecksum(baseCode);
        return $"{baseCode}{checksum}";
    }

    public string GenerateRandomBarcode()
    {
        // Generate a random 12-digit number (prefix 888 for internal)
        var random = new Random();
        var part1 = random.Next(100000000, 999999999);
        var baseCode = $"888{part1}";
        var checksum = CalculateChecksum(baseCode);
        return $"{baseCode}{checksum}";
    }

    public async Task<Product?> FindProductByBarcode(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode))
            return null;

        return await _productRepo
            .FindByCondition(p => p.Barcode == barcode && p.IsActive)
            .Include(p => p.SubCategory)
            .Include(p => p.ProductBatches)
                .ThenInclude(pb => pb.StoreInventories)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> ValidateBarcode(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode))
            return false;

        // Basic validation: check if barcode exists and is unique
        var count = await _productRepo
            .FindByCondition(p => p.Barcode == barcode)
            .CountAsync();

        return count == 1; // Valid if exactly one product has this barcode
    }

    private int CalculateChecksum(string code)
    {
        // Simple checksum calculation (Luhn algorithm variant)
        var sum = 0;
        for (int i = 0; i < code.Length; i++)
        {
            var digit = int.Parse(code[i].ToString());
            sum += i % 2 == 0 ? digit : digit * 3;
        }
        return (10 - sum % 10) % 10;
    }
}
