using PharmaCare.Domain.Models.Products;

namespace PharmaCare.Application.Interfaces.Configuration;

public interface IBarcodeService
{
    string GenerateProductBarcode(int productId);
    string GenerateRandomBarcode();
    Task<Product?> FindProductByBarcode(string barcode);
    Task<bool> ValidateBarcode(string barcode);
}
