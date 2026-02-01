namespace PharmaCare.Application.DTOs.Configuration;

public class ProductPriceDto
{
    public int PriceTypeId { get; set; }
    public string PriceTypeName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsSelected { get; set; }
}
