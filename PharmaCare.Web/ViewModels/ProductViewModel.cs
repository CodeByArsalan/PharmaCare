using PharmaCare.Application.DTOs.Configuration;
using PharmaCare.Domain.Entities.Configuration;

namespace PharmaCare.ViewModels;

public class ProductViewModel : Product
{
    public List<ProductPriceDto> ProductPrices { get; set; } = new();
}
